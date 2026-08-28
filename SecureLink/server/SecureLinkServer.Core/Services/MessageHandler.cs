using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SecureLinkServer.Core.Interfaces;
using SecureLinkServer.Core.Models;

namespace SecureLinkServer.Core.Services;

/// <summary>
/// Обработчик сообщений
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
                Payload = JsonConvert.SerializeObject(new { Error = ex.Message })
            };
            await connection.SendAsync(errorPacket);
        }
    }

    public async Task HandleBinaryDataAsync(IClientConnection connection, byte[] data, string fileName, MessageType type)
    {
        try
        {
            _logger.LogInformation("Received binary data: {FileName} ({Type}, {Size} bytes)", 
                fileName, type, data.Length);

            // Сохраняем файл
            var filePath = await _fileStorage.SaveFileAsync(data, fileName, GetMimeType(fileName), connection.UserId);

            // Создаем сообщение о файле
            var message = new ChatMessage
            {
                SenderId = connection.UserId,
                Type = type,
                Content = filePath,
                FileName = fileName,
                FileSize = data.Length,
                MimeType = GetMimeType(fileName)
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
                    FileName = fileName,
                    Size = data.Length
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

        var chatMessage = new ChatMessage
        {
            ChatId = messageData.ChatId,
            SenderId = connection.UserId,
            Type = MessageType.TextMessage,
            Content = messageData.Text
        };

        await _repository.SaveMessageAsync(chatMessage);

        // Отправляем получателю если он онлайн
        var recipientId = messageData.RecipientId;
        if (!string.IsNullOrEmpty(recipientId))
        {
            var responsePacket = new MessagePacket
            {
                Type = MessageType.TextMessage,
                SenderId = connection.UserId,
                Payload = JsonConvert.SerializeObject(new
                {
                    ChatId = messageData.ChatId,
                    Text = messageData.Text,
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

        var group = new Group
        {
            Name = groupData.Name,
            CreatorId = connection.UserId,
            MemberIds = new List<string> { connection.UserId }
        };

        // Добавляем участников
        if (groupData.MemberIds != null)
        {
            group.MemberIds.AddRange(groupData.MemberIds);
        }

        await _repository.CreateGroupAsync(group);

        // Отправляем подтверждение
        var response = new MessagePacket
        {
            Type = MessageType.GroupInfo,
            Payload = JsonConvert.SerializeObject(new
            {
                GroupId = group.Id,
                Name = group.Name,
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
                        Name = group.Name,
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
