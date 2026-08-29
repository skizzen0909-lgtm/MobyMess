using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SecureLinkServer.Core.Interfaces;
using SecureLinkServer.Core.Models;
using SecureLinkServer.Security;

namespace SecureLinkServer.Core.Services;

/// <summary>
/// Обработчик сообщений с валидацией данных
/// </summary>
public class MessageHandler : IMessageHandler
{
    private readonly IConnectionManager _connectionManager;
    private readonly IDataRepository _repository;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<MessageHandler> _logger;

    public MessageHandler(
        IConnectionManager connectionManager,
        IDataRepository repository,
        IFileStorageService fileStorage,
        ILogger<MessageHandler> logger)
    {
        _connectionManager = connectionManager;
        _repository = repository;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    public async Task HandleMessageAsync(IClientConnection connection, MessagePacket packet)
    {
        try
        {
            // Валидация UUID отправителя
            if (!SecurityValidator.IsValidUuid(connection.UserId))
            {
                _logger.LogWarning("Invalid user UUID: {UserId}", connection.UserId);
                return;
            }

            switch (packet.Type)
            {
                case MessageType.Ping:
                    await HandlePing(connection);
                    break;

                case MessageType.TextMessage:
                    await HandleTextMessage(connection, packet);
                    break;

                case MessageType.CreateGroup:
                    await HandleCreateGroup(connection, packet);
                    break;

                case MessageType.SyncContacts:
                    await HandleSyncContacts(connection, packet);
                    break;

                default:
                    _logger.LogWarning("Unknown message type: {Type}", packet.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message of type {Type}", packet.Type);
            
            // Отправляем ошибку клиенту
            var errorPacket = new MessagePacket
            {
                Type = MessageType.Error,
                Payload = JsonConvert.SerializeObject(new { Error = "Internal server error" })
            };
            await connection.SendAsync(errorPacket);
        }
    }

    public async Task HandleBinaryDataAsync(IClientConnection connection, byte[] data, string fileName, MessageType type)
    {
        try
        {
            // Валидация имени файла (защита от Path Traversal)
            if (!SecurityValidator.IsValidFileName(fileName))
            {
                _logger.LogWarning("Invalid file name from user {UserId}: {FileName}", connection.UserId, fileName);
                return;
            }

            var safeFileName = SecurityValidator.GetSafeFileName(fileName);

            // Проверка размера файла
            if (!SecurityValidator.IsValidFileSize(data.Length))
            {
                _logger.LogWarning("File too large from user {UserId}: {Size} bytes", connection.UserId, data.Length);
                return;
            }

            // Проверка MIME-типа
            var mimeType = GetMimeType(safeFileName);
            if (!SecurityValidator.IsValidMimeType(mimeType))
            {
                _logger.LogWarning("Unsupported MIME type from user {UserId}: {MimeType}", connection.UserId, mimeType);
                return;
            }

            _logger.LogInformation("Received binary data: {FileName} ({Type}, {Size} bytes)", 
                safeFileName, type, data.Length);

            // Сохраняем файл с безопасным именем
            var filePath = await _fileStorage.SaveFileAsync(data, safeFileName, mimeType, connection.UserId);

            // Создаем сообщение о файле
            var message = new ChatMessage
            {
                SenderId = connection.UserId,
                Type = type,
                Content = filePath,
                FileName = safeFileName,
                FileSize = data.Length,
                MimeType = mimeType
            };

            // TODO: Определить chatId или groupId из контекста и сохранить сообщение
            // await _repository.SaveMessageAsync(message);

            // Подтверждаем получение
            var response = new MessagePacket
            {
                Type = type,
                SenderId = connection.UserId,
                Payload = JsonConvert.SerializeObject(new 
                { 
                    FilePath = filePath,
                    FileName = safeFileName,
                    Size = data.Length,
                    MimeType = mimeType
                })
            };
            await connection.SendAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling binary data");
            throw;
        }
    }

    private async Task HandlePing(IClientConnection connection)
    {
        var pong = new MessagePacket
        {
            Type = MessageType.Pong,
            SenderId = connection.UserId
        };
        await connection.SendAsync(pong);
        _logger.LogDebug("Ping/Pong with user {UserId}", connection.UserId);
    }

    private async Task HandleTextMessage(IClientConnection connection, MessagePacket packet)
    {
        if (string.IsNullOrEmpty(packet.Payload))
            return;

        var messageData = JsonConvert.DeserializeObject<TextMessageData>(packet.Payload);
        if (messageData == null)
            return;

        // Валидация текста сообщения
        if (!SecurityValidator.IsValidMessage(messageData.Text, "text"))
        {
            _logger.LogWarning("Invalid message content from user {UserId}", connection.UserId);
            return;
        }

        // Санитизация входных данных
        var sanitizedText = SecurityValidator.SanitizeInput(messageData.Text);

        var chatMessage = new ChatMessage
        {
            ChatId = messageData.ChatId,
            SenderId = connection.UserId,
            Type = MessageType.TextMessage,
            Content = sanitizedText
        };

        await _repository.SaveMessageAsync(chatMessage);

        // Отправляем получателю если он онлайн
        var recipientId = messageData.RecipientId;
        if (!string.IsNullOrEmpty(recipientId))
        {
            // Проверка прав доступа - пользователь может писать только в свои чаты
            var hasAccess = await _repository.HasChatAccessAsync(connection.UserId, messageData.ChatId);
            if (!hasAccess)
            {
                _logger.LogWarning("User {UserId} tried to send message to unauthorized chat {ChatId}", 
                    connection.UserId, messageData.ChatId);
                return;
            }

            var responsePacket = new MessagePacket
            {
                Type = MessageType.TextMessage,
                SenderId = connection.UserId,
                Payload = JsonConvert.SerializeObject(new
                {
                    ChatId = messageData.ChatId,
                    Text = sanitizedText,
                    Timestamp = chatMessage.SentAt.ToUnixTimeMilliseconds()
                })
            };
            await _connectionManager.SendMessageToUserAsync(recipientId, responsePacket);
        }
    }

    private async Task HandleCreateGroup(IClientConnection connection, MessagePacket packet)
    {
        if (string.IsNullOrEmpty(packet.Payload))
            return;

        var groupData = JsonConvert.DeserializeObject<CreateGroupData>(packet.Payload);
        if (groupData == null)
            return;

        // Валидация названия группы
        if (!SecurityValidator.IsValidName(groupData.Name))
        {
            _logger.LogWarning("Invalid group name from user {UserId}", connection.UserId);
            return;
        }

        var groupName = SecurityValidator.SanitizeInput(groupData.Name);

        var group = new Group
        {
            Name = groupName,
            CreatorId = connection.UserId,
            MemberIds = new List<string> { connection.UserId }
        };

        // Добавляем участников с валидацией UUID
        if (groupData.MemberIds != null)
        {
            foreach (var memberId in groupData.MemberIds)
            {
                if (SecurityValidator.IsValidUuid(memberId))
                {
                    group.MemberIds.Add(memberId);
                }
                else
                {
                    _logger.LogWarning("Invalid member UUID in group creation: {MemberId}", memberId);
                }
            }
        }

        await _repository.CreateGroupAsync(group);

        // Отправляем подтверждение
        var response = new MessagePacket
        {
            Type = MessageType.GroupInfo,
            Payload = JsonConvert.SerializeObject(new
            {
                GroupId = group.Id,
                Name = groupName,
                MemberIds = group.MemberIds
            })
        };
        await connection.SendAsync(response);

        // Уведомляем участников группы
        foreach (var memberId in group.MemberIds)
        {
            if (memberId != connection.UserId)
            {
                var notifyPacket = new MessagePacket
                {
                    Type = MessageType.GroupInfo,
                    Payload = JsonConvert.SerializeObject(new
                    {
                        GroupId = group.Id,
                        Name = groupName,
                        CreatorId = connection.UserId,
                        Action = "AddedToGroup"
                    })
                };
                await _connectionManager.SendMessageToUserAsync(memberId, notifyPacket);
            }
        }
    }

    private async Task HandleSyncContacts(IClientConnection connection, MessagePacket packet)
    {
        if (string.IsNullOrEmpty(packet.Payload))
            return;

        var contactsData = JsonConvert.DeserializeObject<ContactsSyncData>(packet.Payload);
        if (contactsData?.Contacts == null)
            return;

        // Проверяем какие контакты зарегистрированы
        foreach (var contact in contactsData.Contacts)
        {
            var existingUser = await _repository.GetUserByPhoneAsync(contact.PhoneNumber);
            contact.IsRegistered = existingUser != null;
            contact.UserId = existingUser?.Id ?? string.Empty;
        }

        // Сохраняем контакты
        await _repository.SyncContactsAsync(connection.UserId, contactsData.Contacts);

        // Отправляем ответ с зарегистрированными контактами
        var registeredContacts = contactsData.Contacts.Where(c => c.IsRegistered).ToList();
        var response = new MessagePacket
        {
            Type = MessageType.ContactsResponse,
            Payload = JsonConvert.SerializeObject(new
            {
                Contacts = registeredContacts.Select(c => new
                {
                    c.PhoneNumber,
                    c.DisplayName,
                    c.UserId
                })
            })
        };
        await connection.SendAsync(response);
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

// Вспомогательные классы для десериализации
public class TextMessageData
{
    public string ChatId { get; set; } = string.Empty;
    public string? RecipientId { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class CreateGroupData
{
    public string Name { get; set; } = string.Empty;
    public List<string>? MemberIds { get; set; }
}

public class ContactsSyncData
{
    public List<Contact> Contacts { get; set; } = new();
}
