using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SecureLink.Server.Core.Data;
using SecureLink.Server.Core.Models;
using SecureLink.Server.Core.Security;

namespace SecureLink.Server.Core.Services;

public class WebSocketServer
{
    private readonly HttpListener _listener;
    private readonly AppDbContext _dbContext;
    private readonly ServerSettings _settings;
    private readonly Dictionary<string, WebSocket> _clients = new();
    private readonly Dictionary<string, string> _userToClient = new(); // userId -> clientId
    private readonly CancellationTokenSource _cts = new();
    private const int MaxMessageLength = 10000;
    private const int MaxContactsToReturn = 500;
    private const int MaxMessagesToReturn = 100;

    public WebSocketServer(AppDbContext dbContext, ServerSettings settings)
    {
        _dbContext = dbContext;
        _settings = settings;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://{_settings.IpAddress}:{_settings.Port}/");
    }

    public async Task StartAsync()
    {
        try
        {
            // Проверка подключения к БД перед запуском
            await _dbContext.Database.CanConnectAsync();
            Console.WriteLine("Подключение к базе данных успешно проверено.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"КРИТИЧЕСКАЯ ОШИБКА: Не удалось подключиться к базе данных. {ex.Message}");
            throw;
        }

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
                    _ = HandleClientAsync(wsContext.WebSocket, context.Request.RemoteEndPoint!.ToString());
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
            catch (Exception ex) when (!_cts.Token.IsCancellationRequested)
            {
                Console.WriteLine($"Ошибка принятия соединения: {ex.Message}");
            }
        }
    }

    private async Task HandleClientAsync(WebSocket webSocket, string clientId)
    {
        var buffer = new byte[1024 * 4];
        string? userId = null;
        
        try
        {
            Console.WriteLine($"Клиент подключен: {clientId}");
            
            while (webSocket.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", _cts.Token);
                        break;
                    }

                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    userId = await ProcessMessageAsync(message, clientId, userId);
                }
                catch (WebSocketException ex)
                {
                    Console.WriteLine($"Ошибка WebSocket для клиента {clientId}: {ex.Message}");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Критическая ошибка клиента {clientId}: {ex.Message}");
        }
        finally
        {
            // Обновление статуса offline при отключении
            if (!string.IsNullOrEmpty(userId))
            {
                try
                {
                    var user = await _dbContext.Users.FindAsync(userId);
                    if (user != null)
                    {
                        user.IsOnline = false;
                        user.LastSeen = DateTime.UtcNow;
                        await _dbContext.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка обновления статуса пользователя {userId}: {ex.Message}");
                }
            }
            
            // Удаляем из словаря userId -> clientId
            if (!string.IsNullOrEmpty(userId) && _userToClient.ContainsKey(userId))
            {
                _userToClient.Remove(userId);
            }
            
            _clients.Remove(clientId);
            webSocket.Dispose();
            Console.WriteLine($"Клиент отключен: {clientId}");
        }
    }

    private async Task<string?> ProcessMessageAsync(string json, string clientId, string? userId)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            if (!root.TryGetProperty("action", out var actionElem))
            {
                Console.WriteLine("Получено сообщение без действия");
                return userId;
            }
            
            var action = actionElem.GetString();
            if (string.IsNullOrEmpty(action))
            {
                Console.WriteLine("Пустое действие в сообщении");
                return userId;
            }

            switch (action)
            {
                case "auth":
                    return await HandleAuthAsync(root, clientId);
                case "send_message":
                    if (!string.IsNullOrEmpty(userId))
                        await HandleSendMessageAsync(root, clientId, userId);
                    else
                        Console.WriteLine("Попытка отправки сообщения без аутентификации");
                    break;
                case "get_contacts":
                    if (!string.IsNullOrEmpty(userId))
                        await HandleGetContactsAsync(root, clientId, userId);
                    break;
                case "sync_contacts":
                    if (!string.IsNullOrEmpty(userId))
                        await HandleSyncContactsAsync(root, clientId, userId);
                    break;
                case "create_group":
                    if (!string.IsNullOrEmpty(userId))
                        await HandleCreateGroupAsync(root, clientId, userId);
                    break;
                case "get_messages":
                    if (!string.IsNullOrEmpty(userId))
                        await HandleGetMessagesAsync(root, clientId, userId);
                    break;
                default:
                    Console.WriteLine($"Неизвестное действие: {action}");
                    break;
            }
            return userId;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Ошибка парсинга JSON от клиента {clientId}: {ex.Message}");
            return userId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка обработки сообщения от клиента {clientId}: {ex.Message}");
            return userId;
        }
    }

    private async Task<string?> HandleAuthAsync(JsonElement root, string clientId)
    {
        try
        {
            if (!root.TryGetProperty("phoneNumber", out var phoneElem))
            {
                await SendErrorAsync(clientId, "Номер телефона не указан");
                return null;
            }

            var phoneNumber = phoneElem.GetString();
            
            // Валидация номера телефона
            if (string.IsNullOrEmpty(phoneNumber) || !SecurityValidator.IsValidPhone(phoneNumber))
            {
                await SendErrorAsync(clientId, "Неверный формат номера телефона");
                return null;
            }

            phoneNumber = SecurityValidator.NormalizePhone(phoneNumber);

            User? user;
            try
            {
                user = await _dbContext.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка БД при поиске пользователя: {ex.Message}");
                await SendErrorAsync(clientId, "Ошибка базы данных");
                return null;
            }
            
            if (user == null)
            {
                user = new User 
                { 
                    Id = Guid.NewGuid().ToString(),
                    PhoneNumber = phoneNumber, 
                    DisplayName = phoneNumber,
                    IsOnline = true,
                    LastSeen = DateTime.UtcNow
                };
                _dbContext.Users.Add(user);
                
                try
                {
                    await _dbContext.SaveChangesAsync();
                    Console.WriteLine($"Зарегистрирован новый пользователь: {user.Id}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка сохранения нового пользователя: {ex.Message}");
                    await SendErrorAsync(clientId, "Ошибка регистрации");
                    return null;
                }
            }
            else
            {
                user.IsOnline = true;
                user.LastSeen = DateTime.UtcNow;
                
                try
                {
                    await _dbContext.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка обновления статуса пользователя: {ex.Message}");
                }
            }

            // Привязка userId к clientId для поиска получателя
            _userToClient[user.Id] = clientId;
            
            if (!_clients.ContainsKey(clientId))
            {
                _clients[clientId] = webSocket;
            }
            
            var response = new 
            { 
                action = "auth_result", 
                success = true, 
                userId = user.Id, 
                displayName = user.DisplayName ?? user.PhoneNumber 
            };
            await SendToClientAsync(clientId, JsonSerializer.Serialize(response));
            
            Console.WriteLine($"Пользователь аутентифицирован: {user.Id} ({user.PhoneNumber})");
            return user.Id;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Критическая ошибка аутентификации: {ex.Message}");
            await SendErrorAsync(clientId, "Внутренняя ошибка сервера");
            return null;
        }
    }

    private async Task HandleSendMessageAsync(JsonElement root, string clientId, string senderId)
    {
        try
        {
            // Извлечение и валидация полей
            if (!root.TryGetProperty("type", out var typeElem) || !root.TryGetProperty("content", out var contentElem))
            {
                await SendErrorAsync(clientId, "Отсутствуют обязательные поля сообщения");
                return;
            }

            var type = typeElem.GetString();
            var content = contentElem.GetString();
            var recipientId = root.TryGetProperty("recipientId", out var r) ? r.GetString() : null;
            var groupId = root.TryGetProperty("groupId", out var g) ? g.GetString() : null;

            // Валидация типа сообщения
            if (string.IsNullOrEmpty(type) || !Enum.TryParse<MessageType>(type, true, out var messageType))
            {
                await SendErrorAsync(clientId, "Неверный тип сообщения");
                return;
            }

            // Валидация содержимого
            if (!SecurityValidator.IsValidMessage(content, type))
            {
                await SendErrorAsync(clientId, "Содержимое сообщения не проходит валидацию");
                return;
            }

            // Санитизация текстовых сообщений
            if (messageType == MessageType.Text && !string.IsNullOrEmpty(content))
            {
                content = SecurityValidator.SanitizeInput(content);
            }

            // Проверка прав доступа (получатель или группа должны существовать)
            if (!string.IsNullOrEmpty(recipientId))
            {
                var recipientExists = await _dbContext.Users.AnyAsync(u => u.Id == recipientId);
                if (!recipientExists)
                {
                    await SendErrorAsync(clientId, "Получатель не найден");
                    return;
                }
            }
            else if (!string.IsNullOrEmpty(groupId))
            {
                var groupExists = await _dbContext.ChatGroups.AnyAsync(g => g.Id == groupId);
                if (!groupExists)
                {
                    await SendErrorAsync(clientId, "Группа не найдена");
                    return;
                }
                
                // Проверка что отправитель является участником группы
                var group = await _dbContext.ChatGroups.FindAsync(groupId);
                if (group == null || !group.MemberIds.Contains(senderId))
                {
                    await SendErrorAsync(clientId, "Вы не являетесь участником этой группы");
                    return;
                }
            }
            else
            {
                await SendErrorAsync(clientId, "Не указан получатель или группа");
                return;
            }

            var message = new Message
            {
                Id = Guid.NewGuid().ToString(),
                SenderId = senderId,
                RecipientId = recipientId,
                GroupId = groupId,
                Type = messageType,
                Content = content!,
                Timestamp = DateTime.UtcNow,
                IsRead = false
            };

            _dbContext.Messages.Add(message);
            
            try
            {
                await _dbContext.SaveChangesAsync();
                Console.WriteLine($"Сообщение сохранено: {message.Id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка сохранения сообщения: {ex.Message}");
                await SendErrorAsync(clientId, "Ошибка сохранения сообщения");
                return;
            }

            // Отправка получателю
            if (!string.IsNullOrEmpty(recipientId))
            {
                var recipientClient = FindClientByUserId(recipientId);
                if (!string.IsNullOrEmpty(recipientClient))
                {
                    var msgJson = JsonSerializer.Serialize(new { action = "new_message", message });
                    await SendToClientAsync(recipientClient, msgJson);
                    Console.WriteLine($"Сообщение отправлено получателю {recipientId}");
                }
                else
                {
                    Console.WriteLine($"Получатель {recipientId} офлайн, сообщение сохранено в БД");
                }
            }
            else if (!string.IsNullOrEmpty(groupId))
            {
                // Рассылка группе
                var group = await _dbContext.ChatGroups.FindAsync(groupId);
                if (group != null)
                {
                    var msgJson = JsonSerializer.Serialize(new { action = "new_message", message });
                    foreach (var memberId in group.MemberIds)
                    {
                        if (memberId != senderId)
                        {
                            var memberClient = FindClientByUserId(memberId);
                            if (!string.IsNullOrEmpty(memberClient))
                            {
                                await SendToClientAsync(memberClient, msgJson);
                                Console.WriteLine($"Сообщение отправлено участнику группы {memberId}");
                            }
                        }
                    }
                }
            }

            var confirmResponse = JsonSerializer.Serialize(new { action = "message_sent", messageId = message.Id });
            await SendToClientAsync(clientId, confirmResponse);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка отправки сообщения: {ex.Message}");
            await SendErrorAsync(clientId, "Ошибка при отправке сообщения");
        }
    }

    private async Task HandleGetContactsAsync(JsonElement root, string clientId, string userId)
    {
        try
        {
            List<Contact> contacts;
            try
            {
                contacts = await _dbContext.Contacts
                    .Where(c => c.UserId == userId)
                    .Take(MaxContactsToReturn)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка получения контактов: {ex.Message}");
                await SendErrorAsync(clientId, "Ошибка получения контактов");
                return;
            }
            
            var phoneNumbers = contacts.Select(c => c.ContactPhoneNumber).Distinct().ToList();
            
            List<User> registeredContacts;
            try
            {
                registeredContacts = await _dbContext.Users
                    .Where(u => phoneNumbers.Contains(u.PhoneNumber))
                    .Select(u => new User 
                    { 
                        Id = u.Id,
                        PhoneNumber = u.PhoneNumber,
                        DisplayName = u.DisplayName,
                        IsOnline = u.IsOnline,
                        LastSeen = u.LastSeen
                    })
                    .Take(MaxContactsToReturn)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка поиска зарегистрированных контактов: {ex.Message}");
                await SendErrorAsync(clientId, "Ошибка поиска контактов");
                return;
            }
            
            var response = new { action = "contacts_list", contacts = registeredContacts };
            await SendToClientAsync(clientId, JsonSerializer.Serialize(response));
            Console.WriteLine($"Отправлено {registeredContacts.Count} контактов пользователю {userId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка обработки запроса контактов: {ex.Message}");
            await SendErrorAsync(clientId, "Ошибка обработки контактов");
        }
    }

    private async Task HandleSyncContactsAsync(JsonElement root, string clientId, string userId)
    {
        try
        {
            if (!root.TryGetProperty("contacts", out var contactsElem))
            {
                await SendErrorAsync(clientId, "Список контактов не указан");
                return;
            }

            // Очищаем старые контакты пользователя
            var oldContacts = await _dbContext.Contacts
                .Where(c => c.UserId == userId)
                .ToListAsync();
            _dbContext.Contacts.RemoveRange(oldContacts);
            
            // Парсим новые контакты из запроса
            var newContacts = new List<Contact>();
            foreach (var contactElem in contactsElem.EnumerateArray())
            {
                if (!contactElem.TryGetProperty("phoneNumber", out var phoneElem) ||
                    !contactElem.TryGetProperty("displayName", out var nameElem))
                {
                    continue;
                }

                var phoneNumber = phoneElem.GetString();
                var displayName = nameElem.GetString();
                
                if (string.IsNullOrEmpty(phoneNumber) || string.IsNullOrEmpty(displayName))
                    continue;

                phoneNumber = SecurityValidator.NormalizePhone(phoneNumber);
                
                // Вычисляем хэш номера для поиска
                var phoneHash = SecurityValidator.HashPhoneNumber(phoneNumber);
                
                // Проверяем, зарегистрирован ли этот контакт
                var isRegistered = await _dbContext.Users.AnyAsync(u => u.PhoneNumber == phoneNumber);
                
                newContacts.Add(new Contact
                {
                    UserId = userId,
                    ContactPhoneNumber = phoneNumber,
                    ContactName = displayName,
                    IsRegistered = isRegistered
                });
            }

            // Сохраняем новые контакты
            if (newContacts.Any())
            {
                _dbContext.Contacts.AddRange(newContacts);
                await _dbContext.SaveChangesAsync();
                Console.WriteLine($"Синхронизировано {newContacts.Count} контактов для пользователя {userId}");
            }

            // Возвращаем список зарегистрированных контактов
            var registeredPhones = newContacts.Where(c => c.IsRegistered).Select(c => c.ContactPhoneNumber).ToList();
            
            var registeredUsers = await _dbContext.Users
                .Where(u => registeredPhones.Contains(u.PhoneNumber))
                .Select(u => new 
                { 
                    Id = u.Id,
                    PhoneNumber = u.PhoneNumber,
                    DisplayName = u.DisplayName,
                    IsOnline = u.IsOnline,
                    LastSeen = u.LastSeen
                })
                .Take(MaxContactsToReturn)
                .ToListAsync();

            var response = new 
            { 
                action = "sync_contacts_result", 
                contacts = registeredUsers,
                totalCount = newContacts.Count,
                registeredCount = registeredUsers.Count
            };
            await SendToClientAsync(clientId, JsonSerializer.Serialize(response));
            Console.WriteLine($"Отправлено {registeredUsers.Count} зарегистрированных контактов пользователю {userId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка синхронизации контактов: {ex.Message}");
            await SendErrorAsync(clientId, "Ошибка синхронизации контактов");
        }
    }

    private async Task HandleCreateGroupAsync(JsonElement root, string clientId, string creatorId)
    {
        try
        {
            if (!root.TryGetProperty("name", out var nameElem) || !root.TryGetProperty("memberIds", out var membersElem))
            {
                await SendErrorAsync(clientId, "Отсутствует название группы или список участников");
                return;
            }

            var name = nameElem.GetString();
            var memberIds = membersElem.EnumerateArray().Select(e => e.GetString()).Where(s => !string.IsNullOrEmpty(s)).ToList();

            // Валидация названия
            if (string.IsNullOrEmpty(name) || !SecurityValidator.IsValidName(name))
            {
                await SendErrorAsync(clientId, "Неверное название группы");
                return;
            }
            
            name = SecurityValidator.SanitizeInput(name);

            if (memberIds.Count == 0)
            {
                await SendErrorAsync(clientId, "Группа должна содержать хотя бы одного участника");
                return;
            }

            // Проверка существования всех участников
            var existingUsers = await _dbContext.Users
                .Where(u => memberIds.Contains(u.Id))
                .Select(u => u.Id)
                .ToListAsync();
            
            if (existingUsers.Count != memberIds.Count)
            {
                await SendErrorAsync(clientId, "Некоторые участники не найдены");
                return;
            }

            // Добавляем создателя в участники, если его там нет
            if (!memberIds.Contains(creatorId))
            {
                memberIds.Add(creatorId);
            }

            var group = new ChatGroup
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                CreatorId = creatorId,
                MemberIds = memberIds,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.ChatGroups.Add(group);
            
            try
            {
                await _dbContext.SaveChangesAsync();
                Console.WriteLine($"Создана группа: {group.Id} ({group.Name})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка создания группы: {ex.Message}");
                await SendErrorAsync(clientId, "Ошибка создания группы");
                return;
            }

            var response = new { action = "group_created", groupId = group.Id, group = new { group.Id, group.Name, group.MemberIds } };
            await SendToClientAsync(clientId, JsonSerializer.Serialize(response));
            
            // Уведомить участников
            var groupNotifyJson = JsonSerializer.Serialize(new { action = "group_added", group = new { group.Id, group.Name, group.MemberIds } });
            foreach (var memberId in memberIds)
            {
                if (memberId != creatorId)
                {
                    var memberClient = FindClientByUserId(memberId);
                    if (!string.IsNullOrEmpty(memberClient))
                    {
                        await SendToClientAsync(memberClient, groupNotifyJson);
                        Console.WriteLine($"Участник {memberId} уведомлен о добавлении в группу");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка создания группы: {ex.Message}");
            await SendErrorAsync(clientId, "Ошибка при создании группы");
        }
    }

    private async Task HandleGetMessagesAsync(JsonElement root, string clientId, string userId)
    {
        try
        {
            var chatId = root.TryGetProperty("chatId", out var c) ? c.GetString() : null;
            var contactId = root.TryGetProperty("contactId", out var cont) ? cont.GetString() : null;
            var groupId = root.TryGetProperty("groupId", out var g) ? g.GetString() : null;
            var limit = root.TryGetProperty("limit", out var l) ? l.GetInt32() : MaxMessagesToReturn;
            
            if (limit > MaxMessagesToReturn || limit <= 0)
                limit = MaxMessagesToReturn;

            List<Message> messages = new();

            if (!string.IsNullOrEmpty(groupId))
            {
                // Проверка доступа к группе
                var group = await _dbContext.ChatGroups.FindAsync(groupId);
                if (group == null || !group.MemberIds.Contains(userId))
                {
                    await SendErrorAsync(clientId, "Нет доступа к этой группе");
                    return;
                }
                
                messages = await _dbContext.Messages
                    .Where(m => m.GroupId == groupId)
                    .OrderByDescending(m => m.Timestamp)
                    .Take(limit)
                    .ToListAsync();
            }
            else if (!string.IsNullOrEmpty(contactId))
            {
                // Личный чат - проверка что это действительно контакт
                var isContact = await _dbContext.Contacts
                    .AnyAsync(c => c.UserId == userId && c.ContactPhoneNumber == contactId);
                
                if (!isContact)
                {
                    // Проверяем обратную ситуацию - может contactId это сам пользователь
                    var user = await _dbContext.Users.FindAsync(contactId);
                    if (user == null)
                    {
                        await SendErrorAsync(clientId, "Контакт не найден");
                        return;
                    }
                    
                    var isContactReverse = await _dbContext.Contacts
                        .AnyAsync(c => c.UserId == contactId && c.ContactPhoneNumber == user.PhoneNumber);
                    
                    if (!isContactReverse)
                    {
                        await SendErrorAsync(clientId, "Нет доступа к этому чату");
                        return;
                    }
                }
                
                var contactUser = await _dbContext.Users.FindAsync(contactId);
                if (contactUser == null)
                {
                    await SendErrorAsync(clientId, "Контакт не найден");
                    return;
                }
                
                messages = await _dbContext.Messages
                    .Where(m => (m.SenderId == userId && m.RecipientId == contactId) || 
                               (m.SenderId == contactId && m.RecipientId == userId))
                    .OrderByDescending(m => m.Timestamp)
                    .Take(limit)
                    .ToListAsync();
            }

            messages.Reverse();
            var response = new { action = "messages_list", messages };
            await SendToClientAsync(clientId, JsonSerializer.Serialize(response));
            Console.WriteLine($"Отправлено {messages.Count} сообщений пользователю {userId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка получения сообщений: {ex.Message}");
            await SendErrorAsync(clientId, "Ошибка получения истории сообщений");
        }
    }

    private string? FindClientByUserId(string userId)
    {
        // Поиск clientId по userId через словарь _userToClient
        if (_userToClient.TryGetValue(userId, out var clientId))
        {
            return clientId;
        }
        return null;
    }

    private async Task SendToClientAsync(string clientId, string message)
    {
        if (_clients.TryGetValue(clientId, out var ws) && ws.State == WebSocketState.Open)
        {
            try
            {
                var buffer = Encoding.UTF8.GetBytes(message);
                await ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка отправки клиенту {clientId}: {ex.Message}");
            }
        }
    }

    private async Task SendErrorAsync(string clientId, string errorMessage)
    {
        var errorResponse = JsonSerializer.Serialize(new { action = "error", message = errorMessage });
        await SendToClientAsync(clientId, errorResponse);
    }

    public void Stop()
    {
        _cts.Cancel();
        _listener.Stop();
        foreach (var client in _clients.Values)
        {
            try
            {
                client.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка закрытия соединения: {ex.Message}");
            }
        }
        Console.WriteLine("Сервер остановлен");
    }
}
