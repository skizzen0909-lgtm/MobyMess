using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SecureLinkServer.Core.Interfaces;
using SecureLinkServer.Core.Models;

namespace SecureLinkServer.Core.Services;

/// <summary>
/// Менеджер подключений клиентов
/// </summary>
public class ConnectionManager : IConnectionManager
{
    private readonly ConcurrentDictionary<string, IClientConnection> _connections = new();
    private readonly ILogger<ConnectionManager> _logger;

    public ConnectionManager(ILogger<ConnectionManager> logger)
    {
        _logger = logger;
    }

    public Task AddConnectionAsync(string userId, IClientConnection connection)
    {
        _connections[userId] = connection;
        _logger.LogInformation("User {UserId} connected. Total connections: {Count}", userId, _connections.Count);
        return Task.CompletedTask;
    }

    public Task RemoveConnectionAsync(string userId)
    {
        if (_connections.TryRemove(userId, out var connection))
        {
            _logger.LogInformation("User {UserId} disconnected. Total connections: {Count}", userId, _connections.Count);
        }
        return Task.CompletedTask;
    }

    public IClientConnection? GetConnection(string userId)
    {
        _connections.TryGetValue(userId, out var connection);
        return connection;
    }

    public IEnumerable<string> GetConnectedUsers()
    {
        return _connections.Keys;
    }

    public async Task SendMessageToUserAsync(string userId, MessagePacket packet)
    {
        if (_connections.TryGetValue(userId, out var connection))
        {
            try
            {
                await connection.SendAsync(packet);
                _logger.LogDebug("Message {MessageId} sent to user {UserId}", packet.MessageId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message to user {UserId}", userId);
            }
        }
        else
        {
            _logger.LogWarning("User {UserId} is not connected, message queued", userId);
            // TODO: Реализовать очередь сообщений для офлайн пользователей
        }
    }

    public async Task SendMessageToGroupAsync(string groupId, MessagePacket packet)
    {
        // TODO: Получить участников группы из репозитория и отправить сообщение всем
        foreach (var connection in _connections.Values)
        {
            try
            {
                await connection.SendAsync(packet);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message to group member");
            }
        }
    }
}

/// <summary>
/// Клиентское подключение через WebSocket
/// </summary>
public class WebSocketClientConnection : IClientConnection
{
    private readonly System.Net.WebSockets.WebSocket _socket;
    private readonly string _userId;
    private readonly IConnectionManager _connectionManager;
    private readonly ILogger<WebSocketClientConnection> _logger;
    private readonly CancellationTokenSource _cts = new();

    public string UserId => _userId;
    public string ConnectionId { get; } = Guid.NewGuid().ToString();
    public DateTime ConnectedAt { get; } = DateTime.UtcNow;

    public WebSocketClientConnection(
        System.Net.WebSockets.WebSocket socket,
        string userId,
        IConnectionManager connectionManager,
        ILogger<WebSocketClientConnection> logger)
    {
        _socket = socket;
        _userId = userId;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    public async Task SendAsync(MessagePacket packet)
    {
        if (_socket.State != System.Net.WebSockets.WebSocketState.Open)
            throw new InvalidOperationException("WebSocket is not open");

        var json = JsonConvert.SerializeObject(packet);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        await _socket.SendAsync(
            new ArraySegment<byte>(bytes),
            System.Net.WebSockets.WebSocketMessageType.Text,
            true,
            _cts.Token);
    }

    public async Task SendBinaryAsync(byte[] data, string fileName, MessageType type)
    {
        if (_socket.State != System.Net.WebSockets.WebSocketState.Open)
            throw new InvalidOperationException("WebSocket is not open");

        // Отправляем метаданные сначала
        var metadata = new
        {
            Type = type,
            FileName = fileName,
            Size = data.Length
        };
        var metadataJson = JsonConvert.SerializeObject(metadata);
        var metadataBytes = System.Text.Encoding.UTF8.GetBytes(metadataJson);

        // Префикс с длиной метаданных
        var metadataLength = BitConverter.GetBytes(metadataBytes.Length);
        var header = new byte[4 + metadataBytes.Length];
        Buffer.BlockCopy(metadataLength, 0, header, 0, 4);
        Buffer.BlockCopy(metadataBytes, 0, header, 4, metadataBytes.Length);

        await _socket.SendAsync(
            new ArraySegment<byte>(header),
            System.Net.WebSockets.WebSocketMessageType.Binary,
            false,
            _cts.Token);

        // Отправляем данные файла чанками
        int chunkSize = 16384; // 16KB
        int offset = 0;
        while (offset < data.Length)
        {
            int size = Math.Min(chunkSize, data.Length - offset);
            bool isLastChunk = offset + size >= data.Length;
            
            await _socket.SendAsync(
                new ArraySegment<byte>(data, offset, size),
                System.Net.WebSockets.WebSocketMessageType.Binary,
                isLastChunk,
                _cts.Token);
            
            offset += size;
        }
    }

    public void Disconnect()
    {
        _cts.Cancel();
        _socket.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None)
            .Wait(TimeSpan.FromSeconds(5));
        _connectionManager.RemoveConnectionAsync(_userId).Wait();
    }
}
