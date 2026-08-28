using System.Net;
using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SecureLinkServer.Config;
using SecureLinkServer.Core.Interfaces;
using SecureLinkServer.Core.Models;
using SecureLinkServer.Core.Services;

namespace SecureLinkServer.Services;

/// <summary>
/// WebSocket сервер для обработки подключений клиентов
/// </summary>
public class WebSocketServerService
{
    private readonly HttpListener _listener;
    private readonly ServerConfig _config;
    private readonly IConnectionManager _connectionManager;
    private readonly IMessageHandler _messageHandler;
    private readonly IAuthService _authService;
    private readonly ILogger<WebSocketServerService> _logger;
    private CancellationTokenSource? _cts;
    private bool _isRunning;

    public WebSocketServerService(
        ServerConfig config,
        IConnectionManager connectionManager,
        IMessageHandler messageHandler,
        IAuthService authService,
        ILogger<WebSocketServerService> logger)
    {
        _config = config;
        _connectionManager = connectionManager;
        _messageHandler = messageHandler;
        _authService = authService;
        _logger = logger;

        var prefix = $"http://{_config.Host}:{_config.Port}/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(prefix);
        
        _logger.LogInformation("WebSocket server configured on {Prefix}", prefix);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_isRunning)
        {
            throw new InvalidOperationException("Server is already running");
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _isRunning = true;

        try
        {
            _listener.Start();
            _logger.LogInformation("WebSocket server started on port {Port}", _config.Port);
            
            Task.Run(() => AcceptConnectionsAsync(_cts.Token));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start WebSocket server");
            _isRunning = false;
            throw;
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (!_isRunning) return;

        _cts?.Cancel();
        _listener.Stop();
        _isRunning = false;

        // Отключаем всех клиентов
        foreach (var userId in _connectionManager.GetConnectedUsers())
        {
            var connection = _connectionManager.GetConnection(userId);
            connection?.Disconnect();
        }

        _logger.LogInformation("WebSocket server stopped");
    }

    private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _isRunning)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                
                // Обрабатываем подключение в фоне
                _ = Task.Run(() => HandleConnectionAsync(context, cancellationToken));
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 995)
            {
                // Listener closed
                break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting connection");
            }
        }
    }

    private async Task HandleConnectionAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        string? userId = null;

        try
        {
            // Проверяем является ли запрос WebSocket
            if (!context.Request.IsWebSocketRequest)
            {
                // Обработка REST API запросов (для аутентификации и т.д.)
                await HandleHttpRequestAsync(context);
                return;
            }

            // Принимаем WebSocket подключение
            var wsContext = await context.AcceptWebSocketAsync(null);
            var webSocket = wsContext.WebSocket;

            // Получаем токен из заголовков или query string
            var token = context.Request.QueryString["token"] 
                ?? context.Request.Headers["Authorization"]?.Replace("Bearer ", "");

            if (string.IsNullOrEmpty(token))
            {
                await CloseWebSocketAsync(webSocket, "No token provided");
                return;
            }

            // Валидируем токен
            var isValid = await _authService.ValidateTokenAsync(token, out userId);
            if (!isValid || string.IsNullOrEmpty(userId))
            {
                await CloseWebSocketAsync(webSocket, "Invalid token");
                return;
            }

            // Создаем подключение
            var connection = new WebSocketClientConnection(
                webSocket,
                userId,
                _connectionManager,
                _logger);

            // Добавляем в менеджер подключений
            await _connectionManager.AddConnectionAsync(userId, connection);

            _logger.LogInformation("Client connected: UserId={UserId}, ConnectionId={ConnectionId}", 
                userId, connection.ConnectionId);

            // Начинаем обработку сообщений
            await ReceiveMessagesAsync(connection, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Connection cancelled for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling connection for user {UserId}", userId);
        }
        finally
        {
            // Очищаем подключение
            if (!string.IsNullOrEmpty(userId))
            {
                await _connectionManager.RemoveConnectionAsync(userId);
            }
        }
    }

    private async Task ReceiveMessagesAsync(WebSocketClientConnection connection, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var receivedData = new MemoryStream();
        
        // Для приёма файлов с chunking - используем состояние на уровне подключения
        var fileReceivingState = connection.FileReceivingState;

        while (!cancellationToken.IsCancellationRequested && 
               connection is { } && 
               (connection as WebSocketClientConnection)?.GetType().GetProperty("_socket")?.GetValue(connection) is System.Net.WebSockets.WebSocket socket &&
               socket.State == System.Net.WebSockets.WebSocketState.Open)
        {
            try
            {
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
                        return;
                    }

                    if (result.Count > 0)
                    {
                        receivedData.Write(buffer, 0, result.Count);
                    }
                } while (!result.EndOfMessage);

                // Обрабатываем полученное сообщение
                if (receivedData.Length > 0)
                {
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var json = Encoding.UTF8.GetString(receivedData.ToArray());
                        _logger.LogDebug("Received JSON: {Json}", json.Length > 500 ? json.Substring(0, 500) + "..." : json);
                        
                        try
                        {
                            using var doc = JsonDocument.Parse(json);
                            var root = doc.RootElement;
                            
                            if (root.TryGetProperty("action", out var actionElement))
                            {
                                var action = actionElement.GetString();
                                
                                switch (action)
                                {
                                    case "send_file_metadata":
                                        await HandleFileMetadataAsync(connection, root);
                                        break;
                                        
                                    case "send_file_chunk":
                                        await HandleFileChunkAsync(connection, root, fileReceivingState);
                                        break;
                                        
                                    default:
                                        // Стандартная обработка через MessageHandler
                                        var packet = JsonConvert.DeserializeObject<MessagePacket>(json);
                                        if (packet != null)
                                        {
                                            await _messageHandler.HandleMessageAsync(connection, packet);
                                        }
                                        break;
                                }
                            }
                            else
                            {
                                // Старый формат без action
                                var packet = JsonConvert.DeserializeObject<MessagePacket>(json);
                                if (packet != null)
                                {
                                    await _messageHandler.HandleMessageAsync(connection, packet);
                                }
                            }
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogError(ex, "Failed to parse JSON message");
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        // Бинарные данные (файлы) - старый формат
                        var data = receivedData.ToArray();
                        
                        // Пытаемся получить метаданные из первых байтов
                        if (data.Length >= 4)
                        {
                            // Простая эвристика: если первые 4 байта это длина JSON
                            var metadataLength = BitConverter.ToInt32(data, 0);
                            if (metadataLength > 0 && metadataLength < data.Length)
                            {
                                var metadataJson = Encoding.UTF8.GetString(data, 4, metadataLength);
                                var metadata = JsonConvert.DeserializeObject<dynamic>(metadataJson);
                                
                                var fileName = metadata?.FileName?.ToString() ?? "unknown.bin";
                                var type = (MessageType)(metadata?.Type?.Value<int>() ?? 11); // Default to FileMessage
                                
                                var fileData = data.Skip(4 + metadataLength).ToArray();
                                
                                await _messageHandler.HandleBinaryDataAsync(connection, fileData, fileName, type);
                            }
                            else
                            {
                                // Просто сохраняем как файл
                                await _messageHandler.HandleBinaryDataAsync(connection, data, "unknown.bin", MessageType.FileMessage);
                            }
                        }
                    }
                }

                receivedData.Clear();
            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                _logger.LogInformation("Client disconnected unexpectedly: {UserId}", connection.UserId);
                break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving message from {UserId}", connection.UserId);
            }
        }
        
        // Очищаем незавершённые загрузки
        foreach (var state in fileReceivingState.Values)
        {
            try
            {
                if (File.Exists(state.TempFilePath))
                {
                    File.Delete(state.TempFilePath);
                }
            }
            catch { }
        }
        fileReceivingState.Clear();
    }
    
    private async Task HandleFileMetadataAsync(IClientConnection connection, JsonElement root)
    {
        var senderId = root.GetProperty("senderId").GetString() ?? connection.UserId;
        var fileName = root.GetProperty("fileName").GetString() ?? "unknown";
        var fileSize = root.GetProperty("fileSize").GetInt64();
        var typeStr = root.GetProperty("type").GetString() ?? "FileMessage";
        var recipientId = root.TryGetProperty("recipientId", out var rProp) ? rProp.GetString() : null;
        var groupId = root.TryGetProperty("groupId", out var gProp) ? gProp.GetString() : null;
        var timestamp = root.TryGetProperty("timestamp", out var tProp) ? tProp.GetInt64() : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
        // Определяем тип сообщения
        var messageType = ParseMessageType(typeStr);
        
        // Создаём состояние для приёма файла
        var tempFileName = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{fileName}");
        var state = new FileReceivingState
        {
            FileName = fileName,
            OriginalFileName = fileName,
            FileType = messageType,
            FileSize = fileSize,
            SenderId = senderId,
            RecipientId = recipientId,
            GroupId = groupId,
            TempFilePath = tempFileName,
            ReceivedBytes = 0,
            ChunksReceived = 0
        };
        
        fileReceivingState[fileName] = state;
        
        _logger.LogInformation("File metadata received: {FileName} ({FileSize} bytes, Type: {Type})", 
            fileName, fileSize, messageType);
        
        // Отправляем подтверждение
        var ackPacket = new MessagePacket
        {
            Type = MessageType.System,
            Payload = JsonConvert.SerializeObject(new
            {
                Action = "file_metadata_ack",
                FileName = fileName,
                Status = "ready"
            })
        };
        await connection.SendAsync(ackPacket);
    }
    
    private async Task HandleFileChunkAsync(IClientConnection connection, JsonElement root, Dictionary<string, FileReceivingState> fileReceivingState)
    {
        var fileName = root.GetProperty("fileName").GetString() ?? throw new ArgumentException("fileName required");
        var chunkIndex = root.GetProperty("chunkIndex").GetInt32();
        var dataBase64 = root.GetProperty("data").GetString() ?? throw new ArgumentException("data required");
        var isLast = root.TryGetProperty("isLast", out var lastProp) && lastProp.GetBoolean();
        
        if (!fileReceivingState.TryGetValue(fileName, out var state))
        {
            _logger.LogWarning("Received chunk for unknown file: {FileName}", fileName);
            return;
        }
        
        try
        {
            // Декодируем base64
            var chunkData = Convert.FromBase64String(dataBase64);
            
            // Дописываем во временный файл
            using var fs = new FileStream(state.TempFilePath, FileMode.Append, FileAccess.Write);
            await fs.WriteAsync(chunkData, 0, chunkData.Length);
            
            state.ReceivedBytes += chunkData.Length;
            state.ChunksReceived++;
            
            _logger.LogDebug("Chunk {ChunkIndex} received for {FileName} ({Received}/{Total} bytes)", 
                chunkIndex, fileName, state.ReceivedBytes, state.FileSize);
            
            // Если это последний чанк - завершаем приём
            if (isLast)
            {
                await FinalizeFileReceptionAsync(connection, state, fileReceivingState);
            }
            else
            {
                // Отправляем подтверждение чанка
                var ackPacket = new MessagePacket
                {
                    Type = MessageType.System,
                    Payload = JsonConvert.SerializeObject(new
                    {
                        Action = "chunk_ack",
                        FileName = fileName,
                        ChunkIndex = chunkIndex,
                        ReceivedBytes = state.ReceivedBytes
                    })
                };
                await connection.SendAsync(ackPacket);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chunk {ChunkIndex} for {FileName}", chunkIndex, fileName);
            
            // Удаляем временный файл при ошибке
            fileReceivingState.Remove(fileName);
            if (File.Exists(state.TempFilePath))
            {
                File.Delete(state.TempFilePath);
            }
            
            // Сообщаем об ошибке клиенту
            var errorPacket = new MessagePacket
            {
                Type = MessageType.Error,
                Payload = JsonConvert.SerializeObject(new
                {
                    Action = "file_error",
                    FileName = fileName,
                    Error = ex.Message
                })
            };
            await connection.SendAsync(errorPacket);
        }
    }
    
    private async Task FinalizeFileReceptionAsync(IClientConnection connection, FileReceivingState state, Dictionary<string, FileReceivingState> fileReceivingState)
    {
        try
        {
            _logger.LogInformation("File reception complete: {FileName} ({Received} bytes)", 
                state.FileName, state.ReceivedBytes);
            
            // Проверяем размер
            if (state.ReceivedBytes != state.FileSize)
            {
                _logger.LogWarning("File size mismatch: expected {Expected}, received {Received}", 
                    state.FileSize, state.ReceivedBytes);
            }
            
            // Читаем файл
            var fileData = await File.ReadAllBytesAsync(state.TempFilePath);
            
            // Сохраняем через FileStorageService
            var filePath = await _fileStorage.SaveFileAsync(fileData, state.OriginalFileName, 
                GetMimeType(state.OriginalFileName), connection.UserId);
            
            // Создаём сообщение о файле
            var message = new ChatMessage
            {
                SenderId = state.SenderId,
                Type = state.FileType,
                Content = filePath,
                FileName = state.OriginalFileName,
                FileSize = state.ReceivedBytes,
                MimeType = GetMimeType(state.OriginalFileName)
            };
            
            // TODO: Определить chatId или groupId и сохранить сообщение
            // await _repository.SaveMessageAsync(message);
            
            // Отправляем подтверждение
            var response = new MessagePacket
            {
                Type = state.FileType,
                SenderId = connection.UserId,
                Payload = JsonConvert.SerializeObject(new 
                { 
                    FilePath = filePath,
                    FileName = state.OriginalFileName,
                    Size = state.ReceivedBytes,
                    Status = "complete"
                })
            };
            await connection.SendAsync(response);
            
            // Если есть recipientId - пересылаем сообщение
            if (!string.IsNullOrEmpty(state.RecipientId))
            {
                var forwardPacket = new MessagePacket
                {
                    Type = state.FileType,
                    SenderId = state.SenderId,
                    Payload = JsonConvert.SerializeObject(new
                    {
                        FilePath = filePath,
                        FileName = state.OriginalFileName,
                        Size = state.ReceivedBytes,
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    })
                };
                await _connectionManager.SendMessageToUserAsync(state.RecipientId, forwardPacket);
            }
        }
        finally
        {
            // Удаляем из состояния и временный файл
            fileReceivingState.Remove(state.FileName);
            if (File.Exists(state.TempFilePath))
            {
                File.Delete(state.TempFilePath);
            }
        }
    }
    
    private MessageType ParseMessageType(string typeStr)
    {
        return typeStr.ToUpperInvariant() switch
        {
            "IMAGE" or "PHOTO" => MessageType.ImageMessage,
            "VIDEO" => MessageType.VideoMessage,
            "AUDIO" or "VOICE" => MessageType.AudioMessage,
            "FILE" or "DOCUMENT" => MessageType.FileMessage,
            _ => MessageType.FileMessage
        };
    }
    
    private string GetMimeType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".mp4" => "video/mp4",
            ".3gp" => "video/3gpp",
            ".webm" => "video/webm",
            ".mp3" => "audio/mpeg",
            ".ogg" => "audio/ogg",
            ".wav" => "audio/wav",
            ".aac" => "audio/aac",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }
}

/// <summary>
/// Состояние приёма файла
/// </summary>
public class FileReceivingState
{
    public string FileName { get; set; } = "";
    public string OriginalFileName { get; set; } = "";
    public MessageType FileType { get; set; }
    public long FileSize { get; set; }
    public string SenderId { get; set; } = "";
    public string? RecipientId { get; set; }
    public string? GroupId { get; set; }
    public string TempFilePath { get; set; } = "";
    public long ReceivedBytes { get; set; }
    public int ChunksReceived { get; set; }
}

public class WebSocketServerService
{
    private readonly HttpListener _listener;
    private readonly ServerConfig _config;
    private readonly IConnectionManager _connectionManager;
    private readonly IMessageHandler _messageHandler;
    private readonly IAuthService _authService;
    private readonly ILogger<WebSocketServerService> _logger;
    private CancellationTokenSource? _cts;
    private bool _isRunning;

    private async Task HandleHttpRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            // Простой REST API для аутентификации
            if (request.Url?.AbsolutePath == "/api/auth" && request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream);
                var json = await reader.ReadToEndAsync();
                var authRequest = JsonConvert.DeserializeObject<AuthRequest>(json);

                if (authRequest != null)
                {
                    var authResponse = await _authService.AuthenticateAsync(authRequest);
                    var responseJson = JsonConvert.SerializeObject(authResponse);
                    var buffer = Encoding.UTF8.GetBytes(responseJson);

                    response.ContentType = "application/json";
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    return;
                }
            }

            // Health check
            if (request.Url?.AbsolutePath == "/health")
            {
                var healthStatus = new { Status = "OK", Timestamp = DateTime.UtcNow };
                var json = JsonConvert.SerializeObject(healthStatus);
                var buffer = Encoding.UTF8.GetBytes(json);

                response.ContentType = "application/json";
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                return;
            }

            // 404 для остальных запросов
            response.StatusCode = 404;
            response.StatusDescription = "Not Found";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling HTTP request");
            response.StatusCode = 500;
            response.StatusDescription = "Internal Server Error";
        }
        finally
        {
            response.Close();
        }
    }

    private async Task CloseWebSocketAsync(System.Net.WebSockets.WebSocket socket, string reason)
    {
        try
        {
            await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, reason, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing WebSocket: {Reason}", reason);
        }
    }
}
