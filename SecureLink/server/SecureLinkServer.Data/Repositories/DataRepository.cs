using System.Data.SQLite;
using SecureLinkServer.Core.Models;
using SecureLinkServer.Core.Interfaces;
using SecureLinkServer.Data.Entities;

namespace SecureLinkServer.Data.Repositories;

/// <summary>
/// Репозиторий для работы с данными через SQLite
/// </summary>
public class DataRepository : IDataRepository
{
    private readonly DatabaseContext _dbContext;

    public DataRepository(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    // ==================== Пользователи ====================

    public async Task<User?> GetUserByPhoneAsync(string phoneNumber)
    {
        using var cmd = _dbContext.GetConnection().CreateCommand();
        cmd.CommandText = "SELECT * FROM Users WHERE PhoneNumber = @PhoneNumber";
        cmd.Parameters.AddWithValue("@PhoneNumber", phoneNumber);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapUser(reader);
        }
        return null;
    }

    public async Task<User?> GetUserByIdAsync(string userId)
    {
        using var cmd = _dbContext.GetConnection().CreateCommand();
        cmd.CommandText = "SELECT * FROM Users WHERE Id = @Id";
        cmd.Parameters.AddWithValue("@Id", userId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapUser(reader);
        }
        return null;
    }

    public async Task<User> CreateUserAsync(User user)
    {
        using var cmd = _dbContext.GetConnection().CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Users (Id, PhoneNumber, DisplayName, AvatarPath, RegisteredAt, IsActive)
            VALUES (@Id, @PhoneNumber, @DisplayName, @AvatarPath, @RegisteredAt, @IsActive)
        ";
        cmd.Parameters.AddWithValue("@Id", user.Id);
        cmd.Parameters.AddWithValue("@PhoneNumber", user.PhoneNumber);
        cmd.Parameters.AddWithValue("@DisplayName", (object?)user.DisplayName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AvatarPath", (object?)user.AvatarPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@RegisteredAt", user.RegisteredAt.ToString("o"));
        cmd.Parameters.AddWithValue("@IsActive", user.IsActive ? 1 : 0);

        await cmd.ExecuteNonQueryAsync();
        return user;
    }

    public async Task UpdateUserAsync(User user)
    {
        using var cmd = _dbContext.GetConnection().CreateCommand();
        cmd.CommandText = @"
            UPDATE Users SET DisplayName = @DisplayName, AvatarPath = @AvatarPath, IsActive = @IsActive
            WHERE Id = @Id
        ";
        cmd.Parameters.AddWithValue("@Id", user.Id);
        cmd.Parameters.AddWithValue("@DisplayName", (object?)user.DisplayName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AvatarPath", (object?)user.AvatarPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IsActive", user.IsActive ? 1 : 0);

        await cmd.ExecuteNonQueryAsync();
    }

    // ==================== Чаты ====================

    public async Task<Chat?> GetChatBetweenUsersAsync(string user1Id, string user2Id)
    {
        using var cmd = _dbContext.GetConnection().CreateCommand();
        cmd.CommandText = @"
            SELECT * FROM Chats 
            WHERE (User1Id = @User1Id AND User2Id = @User2Id) 
               OR (User1Id = @User2Id AND User2Id = @User1Id)
        ";
        cmd.Parameters.AddWithValue("@User1Id", user1Id);
        cmd.Parameters.AddWithValue("@User2Id", user2Id);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapChat(reader);
        }
        return null;
    }

    public async Task<Chat> CreateChatAsync(Chat chat)
    {
        using var cmd = _dbContext.GetConnection().CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Chats (Id, User1Id, User2Id, CreatedAt, LastMessageAt)
            VALUES (@Id, @User1Id, @User2Id, @CreatedAt, @LastMessageAt)
        ";
        cmd.Parameters.AddWithValue("@Id", chat.Id);
        cmd.Parameters.AddWithValue("@User1Id", chat.User1Id);
        cmd.Parameters.AddWithValue("@User2Id", chat.User2Id);
        cmd.Parameters.AddWithValue("@CreatedAt", chat.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@LastMessageAt", (object?)chat.LastMessageAt?.ToString("o") ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
        return chat;
    }

    public async Task<List<Chat>> GetUserChatsAsync(string userId)
    {
        var chats = new List<Chat>();
        using var cmd = _dbContext.GetConnection().CreateCommand();
        cmd.CommandText = "SELECT * FROM Chats WHERE User1Id = @UserId OR User2Id = @UserId ORDER BY LastMessageAt DESC";
        cmd.Parameters.AddWithValue("@UserId", userId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            chats.Add(MapChat(reader));
        }
        return chats;
    }

    // ==================== Группы ====================

    public async Task<Group?> GetGroupByIdAsync(string groupId)
    {
        Group? group = null;

        using var cmd = _dbContext.GetConnection().CreateCommand();
        cmd.CommandText = "SELECT * FROM Groups WHERE Id = @Id";
        cmd.Parameters.AddWithValue("@Id", groupId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            group = MapGroup(reader);
        }

        if (group != null)
        {
            group.MemberIds = await GetGroupMembersAsync(groupId);
        }

        return group;
    }

    private async Task<List<string>> GetGroupMembersAsync(string groupId)
    {
        var members = new List<string>();
        using var cmd = _dbContext.GetConnection().CreateCommand();
        cmd.CommandText = "SELECT UserId FROM GroupMembers WHERE GroupId = @GroupId";
        cmd.Parameters.AddWithValue("@GroupId", groupId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            members.Add(reader.GetString(0));
        }
        return members;
    }

    public async Task<Group> CreateGroupAsync(Group group)
    {
        using var cmd = _dbContext.GetConnection().CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Groups (Id, Name, CreatorId, CreatedAt, AvatarPath)
            VALUES (@Id, @Name, @CreatorId, @CreatedAt, @AvatarPath)
        ";
        cmd.Parameters.AddWithValue("@Id", group.Id);
        cmd.Parameters.AddWithValue("@Name", group.Name);
        cmd.Parameters.AddWithValue("@CreatorId", group.CreatorId);
        cmd.Parameters.AddWithValue("@CreatedAt", group.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@AvatarPath", (object?)group.AvatarPath ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync();

        // Добавляем создателя как первого участника
        await AddMemberToGroupAsync(group.Id, group.CreatorId);

        return group;
    }

    public async Task AddMemberToGroupAsync(string groupId, string userId)
    {
        using var cmd = _dbContext.GetConnection().CreateCommand();
        cmd.CommandText = @"
            INSERT OR IGNORE INTO GroupMembers (GroupId, UserId, JoinedAt)
            VALUES (@GroupId, @UserId, @JoinedAt)
        ";
        cmd.Parameters.AddWithValue("@GroupId", groupId);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@JoinedAt", DateTime.UtcNow.ToString("o"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RemoveMemberFromGroupAsync(string groupId, string userId)
    {
        using var cmd = _dbContext.GetConnection().CreateCommand();
        cmd.CommandText = "DELETE FROM GroupMembers WHERE GroupId = @GroupId AND UserId = @UserId";
        cmd.Parameters.AddWithValue("@GroupId", groupId);
        cmd.Parameters.AddWithValue("@UserId", userId);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<Group>> GetUserGroupsAsync(string userId)
    {
        var groups = new List<Group>();
        using var cmd = _dbContext.GetConnection().CreateCommand();
        cmd.CommandText = @"
            SELECT g.* FROM Groups g
            INNER JOIN GroupMembers gm ON g.Id = gm.GroupId
            WHERE gm.UserId = @UserId
        ";
        cmd.Parameters.AddWithValue("@UserId", userId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var group = MapGroup(reader);
            group.MemberIds = await GetGroupMembersAsync(group.Id);
            groups.Add(group);
        }
        return groups;
    }

    // ==================== Сообщения ====================

    public async Task SaveMessageAsync(ChatMessage message)
    {
        using var cmd = _dbContext.GetConnection().CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Messages (Id, ChatId, GroupId, SenderId, Type, Content, FileName, FileSize, MimeType, SentAt, IsDelivered, IsRead)
            VALUES (@Id, @ChatId, @GroupId, @SenderId, @Type, @Content, @FileName, @FileSize, @MimeType, @SentAt, @IsDelivered, @IsRead)
        ";
        cmd.Parameters.AddWithValue("@Id", message.Id);
        cmd.Parameters.AddWithValue("@ChatId", (object?)message.ChatId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@GroupId", (object?)message.GroupId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SenderId", message.SenderId);
        cmd.Parameters.AddWithValue("@Type", (int)message.Type);
        cmd.Parameters.AddWithValue("@Content", message.Content);
        cmd.Parameters.AddWithValue("@FileName", (object?)message.FileName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@FileSize", (object?)message.FileSize ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@MimeType", (object?)message.MimeType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SentAt", message.SentAt.ToString("o"));
        cmd.Parameters.AddWithValue("@IsDelivered", message.IsDelivered ? 1 : 0);
        cmd.Parameters.AddWithValue("@IsRead", message.IsRead ? 1 : 0);

        await cmd.ExecuteNonQueryAsync();

        // Обновляем время последнего сообщения в чате
        if (!string.IsNullOrEmpty(message.ChatId))
        {
            using var updateCmd = _dbContext.GetConnection().CreateCommand();
            updateCmd.CommandText = "UPDATE Chats SET LastMessageAt = @LastMessageAt WHERE Id = @ChatId";
            updateCmd.Parameters.AddWithValue("@LastMessageAt", message.SentAt.ToString("o"));
            updateCmd.Parameters.AddWithValue("@ChatId", message.ChatId);
            await updateCmd.ExecuteNonQueryAsync();
        }
    }

    public async Task<List<ChatMessage>> GetChatMessagesAsync(string chatId, int count = 50)
    {
        var messages = new List<ChatMessage>();
        using var cmd = _dbContext.GetConnection().CreateCommand();
        cmd.CommandText = @"
            SELECT * FROM Messages 
            WHERE ChatId = @ChatId 
            ORDER BY SentAt DESC 
            LIMIT @Count
        ";
        cmd.Parameters.AddWithValue("@ChatId", chatId);
        cmd.Parameters.AddWithValue("@Count", count);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            messages.Add(MapMessage(reader));
        }
        messages.Reverse();
        return messages;
    }

    public async Task<List<ChatMessage>> GetGroupMessagesAsync(string groupId, int count = 50)
    {
        var messages = new List<ChatMessage>();
        using var cmd = _dbContext.GetConnection().CreateCommand();
        cmd.CommandText = @"
            SELECT * FROM Messages 
            WHERE GroupId = @GroupId 
            ORDER BY SentAt DESC 
            LIMIT @Count
        ";
        cmd.Parameters.AddWithValue("@GroupId", groupId);
        cmd.Parameters.AddWithValue("@Count", count);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            messages.Add(MapMessage(reader));
        }
        messages.Reverse();
        return messages;
    }

    public async Task MarkMessageAsDeliveredAsync(string messageId)
    {
        using var cmd = _dbContext.GetConnection().CreateCommand();
        cmd.CommandText = "UPDATE Messages SET IsDelivered = 1 WHERE Id = @Id";
        cmd.Parameters.AddWithValue("@Id", messageId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task MarkMessageAsReadAsync(string messageId)
    {
        using var cmd = _dbContext.GetConnection().CreateCommand();
        cmd.CommandText = "UPDATE Messages SET IsRead = 1 WHERE Id = @Id";
        cmd.Parameters.AddWithValue("@Id", messageId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ==================== Контакты ====================

    public async Task<List<Contact>> GetContactsAsync(string userId)
    {
        var contacts = new List<Contact>();
        using var cmd = _dbContext.GetConnection().CreateCommand();
        cmd.CommandText = "SELECT * FROM Contacts WHERE UserId = @UserId";
        cmd.Parameters.AddWithValue("@UserId", userId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            contacts.Add(MapContact(reader));
        }
        return contacts;
    }

    public async Task SyncContactsAsync(string userId, List<Contact> contacts)
    {
        foreach (var contact in contacts)
        {
            using var cmd = _dbContext.GetConnection().CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO Contacts (UserId, ContactPhoneNumber, ContactDisplayName, IsRegistered, SyncedAt)
                VALUES (@UserId, @ContactPhoneNumber, @ContactDisplayName, @IsRegistered, @SyncedAt)
            ";
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@ContactPhoneNumber", contact.PhoneNumber);
            cmd.Parameters.AddWithValue("@ContactDisplayName", contact.DisplayName);
            cmd.Parameters.AddWithValue("@IsRegistered", contact.IsRegistered ? 1 : 0);
            cmd.Parameters.AddWithValue("@SyncedAt", DateTime.UtcNow.ToString("o"));

            await cmd.ExecuteNonQueryAsync();
        }
    }

    // ==================== Проверка прав доступа ====================

    public async Task<bool> HasChatAccessAsync(string userId, string chatId)
    {
        using var cmd = _dbContext.GetConnection().CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Chats WHERE Id = @ChatId AND (User1Id = @UserId OR User2Id = @UserId)";
        cmd.Parameters.AddWithValue("@ChatId", chatId);
        cmd.Parameters.AddWithValue("@UserId", userId);

        var result = await cmd.ExecuteScalarAsync();
        return result is long count && count > 0;
    }

    public async Task<bool> HasGroupAccessAsync(string userId, string groupId)
    {
        using var cmd = _dbContext.GetConnection().CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM GroupMembers WHERE GroupId = @GroupId AND UserId = @UserId";
        cmd.Parameters.AddWithValue("@GroupId", groupId);
        cmd.Parameters.AddWithValue("@UserId", userId);

        var result = await cmd.ExecuteScalarAsync();
        return result is long count && count > 0;
    }

    // ==================== Мапперы ====================

    private static User MapUser(SQLiteDataReader reader) => new()
    {
        Id = reader.GetString("Id"),
        PhoneNumber = reader.GetString("PhoneNumber"),
        DisplayName = reader.IsDBNull("DisplayName") ? null : reader.GetString("DisplayName"),
        AvatarPath = reader.IsDBNull("AvatarPath") ? null : reader.GetString("AvatarPath"),
        RegisteredAt = DateTime.Parse(reader.GetString("RegisteredAt")),
        IsActive = reader.GetInt32("IsActive") == 1
    };

    private static Chat MapChat(SQLiteDataReader reader) => new()
    {
        Id = reader.GetString("Id"),
        User1Id = reader.GetString("User1Id"),
        User2Id = reader.GetString("User2Id"),
        CreatedAt = DateTime.Parse(reader.GetString("CreatedAt")),
        LastMessageAt = reader.IsDBNull("LastMessageAt") ? null : DateTime.Parse(reader.GetString("LastMessageAt"))
    };

    private static Group MapGroup(SQLiteDataReader reader) => new()
    {
        Id = reader.GetString("Id"),
        Name = reader.GetString("Name"),
        CreatorId = reader.GetString("CreatorId"),
        CreatedAt = DateTime.Parse(reader.GetString("CreatedAt")),
        AvatarPath = reader.IsDBNull("AvatarPath") ? null : reader.GetString("AvatarPath"),
        MemberIds = new List<string>()
    };

    private static ChatMessage MapMessage(SQLiteDataReader reader) => new()
    {
        Id = reader.GetString("Id"),
        ChatId = reader.IsDBNull("ChatId") ? null : reader.GetString("ChatId"),
        GroupId = reader.IsDBNull("GroupId") ? null : reader.GetString("GroupId"),
        SenderId = reader.GetString("SenderId"),
        Type = (MessageType)reader.GetInt32("Type"),
        Content = reader.GetString("Content"),
        FileName = reader.IsDBNull("FileName") ? null : reader.GetString("FileName"),
        FileSize = reader.IsDBNull("FileSize") ? null : reader.GetInt64("FileSize"),
        MimeType = reader.IsDBNull("MimeType") ? null : reader.GetString("MimeType"),
        SentAt = DateTime.Parse(reader.GetString("SentAt")),
        IsDelivered = reader.GetInt32("IsDelivered") == 1,
        IsRead = reader.GetInt32("IsRead") == 1
    };

    private static Contact MapContact(SQLiteDataReader reader) => new()
    {
        UserId = reader.GetString("UserId"),
        PhoneNumber = reader.GetString("ContactPhoneNumber"),
        DisplayName = reader.GetString("ContactDisplayName"),
        IsRegistered = reader.GetInt32("IsRegistered") == 1
    };
}
