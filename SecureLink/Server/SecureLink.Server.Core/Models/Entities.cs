using System;

namespace SecureLink.Server.Core.Models;

public enum MessageType
{
    Text,
    Image,
    Video,
    Audio,
    File,
    System
}

public class Message
{
    public long Id { get; set; }
    public string SenderId { get; set; } = string.Empty;
    public string? RecipientId { get; set; } // null для групп
    public string? GroupId { get; set; } // null для личных
    public MessageType Type { get; set; }
    public string Content { get; set; } = string.Empty; // Текст или путь к файлу
    public string? FileName { get; set; }
    public long FileSize { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PhoneNumber { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarPath { get; set; }
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public bool IsOnline { get; set; }
}

public class Contact
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string ContactPhoneNumber { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public bool IsRegistered { get; set; }
}

public class ChatGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string CreatorId { get; set; } = string.Empty;
    public List<string> MemberIds { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ServerSettings
{
    public int Port { get; set; } = 8080;
    public string DatabasePath { get; set; } = "data/messages.db";
    public string FilesPath { get; set; } = "data/files";
    public int MaxFileSizeMb { get; set; } = 100;
    public string IpAddress { get; set; } = "0.0.0.0";
}
