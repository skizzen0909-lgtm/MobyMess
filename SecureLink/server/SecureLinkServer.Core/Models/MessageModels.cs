namespace SecureLinkServer.Core.Models;

/// <summary>
/// Типы сообщений в протоколе SecureLink
/// </summary>
public enum MessageType
{
    // Системные сообщения
    Auth = 1,           // Аутентификация
    AuthResponse = 2,   // Ответ на аутентификацию
    Ping = 3,           // Проверка соединения
    Pong = 4,           // Ответ на ping
    
    // Сообщения чата
    TextMessage = 10,       // Текстовое сообщение
    FileMessage = 11,       // Файл
    ImageMessage = 12,      // Изображение
    VoiceMessage = 13,      // Голосовое сообщение
    VideoMessage = 14,      // Видео
    
    // Группы
    CreateGroup = 20,       // Создание группы
    AddToGroup = 21,        // Добавление в группу
    GroupInfo = 22,         // Информация о группе
    
    // Контакты
    SyncContacts = 30,      // Синхронизация контактов
    ContactsResponse = 31,  // Ответ с контактами
    
    // Ошибки
    Error = 100             // Сообщение об ошибке
}

/// <summary>
/// Базовая модель пакета данных
/// </summary>
public class MessagePacket
{
    public MessageType Type { get; set; }
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    public string? SenderId { get; set; }
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public string? Payload { get; set; }  // JSON данные
}

/// <summary>
/// Модель пользователя
/// </summary>
public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PhoneNumber { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? AvatarPath { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; }
}

/// <summary>
/// Модель контакта
/// </summary>
public class Contact
{
    public string UserId { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsRegistered { get; set; }  // Зарегистрирован ли в мессенджере
}

/// <summary>
/// Модель чата (личного)
/// </summary>
public class Chat
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string User1Id { get; set; } = string.Empty;
    public string User2Id { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastMessageAt { get; set; }
}

/// <summary>
/// Модель группы
/// </summary>
public class Group
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string CreatorId { get; set; } = string.Empty;
    public List<string> MemberIds { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? AvatarPath { get; set; }
}

/// <summary>
/// Модель сообщения
/// </summary>
public class ChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ChatId { get; set; } = string.Empty;
    public string? GroupId { get; set; }
    public string SenderId { get; set; } = string.Empty;
    public MessageType Type { get; set; }
    public string Content { get; set; } = string.Empty;  // Текст или путь к файлу
    public string? FileName { get; set; }
    public long? FileSize { get; set; }
    public string? MimeType { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool IsDelivered { get; set; }
    public bool IsRead { get; set; }
}

/// <summary>
/// Модель для аутентификации
/// </summary>
public class AuthRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string? VerificationCode { get; set; }
}

/// <summary>
/// Ответ на аутентификацию
/// </summary>
public class AuthResponse
{
    public bool Success { get; set; }
    public string? UserId { get; set; }
    public string? Token { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Модель файла
/// </summary>
public class FileData
{
    public string FileName { get; set; } = string.Empty;
    public long Size { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public byte[]? Content { get; set; }
    public string? FilePath { get; set; }  // Путь после сохранения
}
