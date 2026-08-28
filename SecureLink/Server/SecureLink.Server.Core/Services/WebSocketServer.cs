using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SecureLink.Server.Core.Data;
using SecureLink.Server.Core.Models;

namespace SecureLink.Server.Core.Services;

public class WebSocketServer
{
    private readonly HttpListener _listener;
    private readonly AppDbContext _dbContext;
    private readonly ServerSettings _settings;
    private readonly Dictionary<string, WebSocket> _clients = new();
    private readonly CancellationTokenSource _cts = new();

    public WebSocketServer(AppDbContext dbContext, ServerSettings settings)
    {
        _dbContext = dbContext;
        _settings = settings;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://{_settings.IpAddress}:{_settings.Port}/");
    }

    public async Task StartAsync()
    {
        _listener.Start();
        Console.WriteLine($"Сервер запущен на {_settings.IpAddress}:{_settings.Port}");

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    var wsContext = await context.AcceptWebSocketAsync(null);
                    await HandleClientAsync(wsContext.WebSocket, context.Request.RemoteEndPoint!.ToString());
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
            catch (Exception ex) when (!_cts.Token.IsCancellationRequested)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
        }
    }

    private async Task HandleClientAsync(WebSocket webSocket, string clientId)
    {
        var buffer = new byte[1024 * 4];
        _clients[clientId] = webSocket;
        Console.WriteLine($"Клиент подключен: {clientId}");

        try
        {
            while (webSocket.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", _cts.Token);
                    break;
                }

                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                await ProcessMessageAsync(message, clientId);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка клиента {clientId}: {ex.Message}");
        }
        finally
        {
            _clients.Remove(clientId);
            webSocket.Dispose();
            Console.WriteLine($"Клиент отключен: {clientId}");
        }
    }

    private async Task ProcessMessageAsync(string json, string clientId)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var action = root.GetProperty("action").GetString();

            switch (action)
            {
                case "auth":
                    await HandleAuthAsync(root, clientId);
                    break;
                case "send_message":
                    await HandleSendMessageAsync(root, clientId);
                    break;
                case "get_contacts":
                    await HandleGetContactsAsync(root, clientId);
                    break;
                case "create_group":
                    await HandleCreateGroupAsync(root, clientId);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка обработки сообщения: {ex.Message}");
        }
    }

    private async Task HandleAuthAsync(JsonElement root, string clientId)
    {
        var phoneNumber = root.GetProperty("phoneNumber").GetString();
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
        
        if (user == null)
        {
            user = new User { PhoneNumber = phoneNumber!, DisplayName = phoneNumber };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
        }

        user.IsOnline = true;
        user.LastSeen = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        var response = new { action = "auth_result", success = true, userId = user.Id, displayName = user.DisplayName };
        await SendToClientAsync(clientId, JsonSerializer.Serialize(response));
    }

    private async Task HandleSendMessageAsync(JsonElement root, string clientId)
    {
        var senderId = root.GetProperty("senderId").GetString();
        var type = root.GetProperty("type").GetString();
        var content = root.GetProperty("content").GetString();
        var recipientId = root.TryGetProperty("recipientId", out var r) ? r.GetString() : null;
        var groupId = root.TryGetProperty("groupId", out var g) ? g.GetString() : null;

        var message = new Message
        {
            SenderId = senderId!,
            RecipientId = recipientId,
            GroupId = groupId,
            Type = Enum.Parse<MessageType>(type!),
            Content = content!,
            Timestamp = DateTime.UtcNow
        };

        _dbContext.Messages.Add(message);
        await _dbContext.SaveChangesAsync();

        // Отправка получателю
        if (!string.IsNullOrEmpty(recipientId))
        {
            var recipientClient = _clients.FirstOrDefault(c => c.Value.Tag?.ToString() == recipientId).Key;
            if (!string.IsNullOrEmpty(recipientClient))
            {
                await SendToClientAsync(recipientClient, JsonSerializer.Serialize(new { action = "new_message", message }));
            }
        }
        else if (!string.IsNullOrEmpty(groupId))
        {
            // Рассылка группе
            var group = await _dbContext.ChatGroups.FindAsync(groupId);
            if (group != null)
            {
                foreach (var memberId in group.MemberIds)
                {
                    if (memberId != senderId)
                    {
                        var memberClient = _clients.FirstOrDefault(c => c.Value.Tag?.ToString() == memberId).Key;
                        if (!string.IsNullOrEmpty(memberClient))
                        {
                            await SendToClientAsync(memberClient, JsonSerializer.Serialize(new { action = "new_message", message }));
                        }
                    }
                }
            }
        }

        await SendToClientAsync(clientId, JsonSerializer.Serialize(new { action = "message_sent", messageId = message.Id }));
    }

    private async Task HandleGetContactsAsync(JsonElement root, string clientId)
    {
        var userId = root.GetProperty("userId").GetString();
        var contacts = await _dbContext.Contacts.Where(c => c.UserId == userId).ToListAsync();
        var registeredContacts = await _dbContext.Users.Where(u => contacts.Select(c => c.ContactPhoneNumber).Contains(u.PhoneNumber)).ToListAsync();
        
        var response = new { action = "contacts_list", contacts = registeredContacts };
        await SendToClientAsync(clientId, JsonSerializer.Serialize(response));
    }

    private async Task HandleCreateGroupAsync(JsonElement root, string clientId)
    {
        var name = root.GetProperty("name").GetString();
        var creatorId = root.GetProperty("creatorId").GetString();
        var memberIds = root.GetProperty("memberIds").EnumerateArray().Select(e => e.GetString()!).ToList();

        var group = new ChatGroup
        {
            Name = name!,
            CreatorId = creatorId!,
            MemberIds = memberIds
        };

        _dbContext.ChatGroups.Add(group);
        await _dbContext.SaveChangesAsync();

        var response = new { action = "group_created", groupId = group.Id, group };
        await SendToClientAsync(clientId, JsonSerializer.Serialize(response));
        
        // Уведомить участников
        foreach (var memberId in memberIds)
        {
            if (memberId != creatorId)
            {
                var memberClient = _clients.FirstOrDefault(c => c.Value.Tag?.ToString() == memberId).Key;
                if (!string.IsNullOrEmpty(memberClient))
                {
                    await SendToClientAsync(memberClient, JsonSerializer.Serialize(new { action = "group_added", group }));
                }
            }
        }
    }

    private async Task SendToClientAsync(string clientId, string message)
    {
        if (_clients.TryGetValue(clientId, out var ws) && ws.State == WebSocketState.Open)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            await ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }

    public void Stop()
    {
        _cts.Cancel();
        _listener.Stop();
        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }
    }
}
