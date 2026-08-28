using System.Data.SQLite;

namespace SecureLinkServer.Data.Entities;

/// <summary>
/// SQLite контекст базы данных
/// </summary>
public class DatabaseContext : IDisposable
{
    private readonly string _connectionString;
    private SQLiteConnection? _connection;

    public DatabaseContext(string dbPath)
    {
        _connectionString = $"Data Source={dbPath};Version=3;";
    }

    public SQLiteConnection GetConnection()
    {
        if (_connection == null)
        {
            _connection = new SQLiteConnection(_connectionString);
            _connection.Open();
            InitializeDatabase();
        }
        return _connection;
    }

    private void InitializeDatabase()
    {
        using var cmd = GetConnection().CreateCommand();
        cmd.CommandText = @"
            -- Таблица пользователей
            CREATE TABLE IF NOT EXISTS Users (
                Id TEXT PRIMARY KEY,
                PhoneNumber TEXT UNIQUE NOT NULL,
                DisplayName TEXT,
                AvatarPath TEXT,
                RegisteredAt TEXT NOT NULL,
                IsActive INTEGER DEFAULT 1
            );

            -- Таблица чатов
            CREATE TABLE IF NOT EXISTS Chats (
                Id TEXT PRIMARY KEY,
                User1Id TEXT NOT NULL,
                User2Id TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                LastMessageAt TEXT,
                FOREIGN KEY(User1Id) REFERENCES Users(Id),
                FOREIGN KEY(User2Id) REFERENCES Users(Id),
                UNIQUE(User1Id, User2Id)
            );

            -- Таблица групп
            CREATE TABLE IF NOT EXISTS Groups (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                CreatorId TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                AvatarPath TEXT,
                FOREIGN KEY(CreatorId) REFERENCES Users(Id)
            );

            -- Таблица участников групп
            CREATE TABLE IF NOT EXISTS GroupMembers (
                GroupId TEXT NOT NULL,
                UserId TEXT NOT NULL,
                JoinedAt TEXT NOT NULL,
                PRIMARY KEY(GroupId, UserId),
                FOREIGN KEY(GroupId) REFERENCES Groups(Id),
                FOREIGN KEY(UserId) REFERENCES Users(Id)
            );

            -- Таблица сообщений
            CREATE TABLE IF NOT EXISTS Messages (
                Id TEXT PRIMARY KEY,
                ChatId TEXT,
                GroupId TEXT,
                SenderId TEXT NOT NULL,
                Type INTEGER NOT NULL,
                Content TEXT NOT NULL,
                FileName TEXT,
                FileSize INTEGER,
                MimeType TEXT,
                SentAt TEXT NOT NULL,
                IsDelivered INTEGER DEFAULT 0,
                IsRead INTEGER DEFAULT 0,
                FOREIGN KEY(ChatId) REFERENCES Chats(Id),
                FOREIGN KEY(GroupId) REFERENCES Groups(Id),
                FOREIGN KEY(SenderId) REFERENCES Users(Id)
            );

            -- Таблица контактов
            CREATE TABLE IF NOT EXISTS Contacts (
                UserId TEXT NOT NULL,
                ContactPhoneNumber TEXT NOT NULL,
                ContactDisplayName TEXT NOT NULL,
                IsRegistered INTEGER DEFAULT 0,
                SyncedAt TEXT NOT NULL,
                PRIMARY KEY(UserId, ContactPhoneNumber),
                FOREIGN KEY(UserId) REFERENCES Users(Id)
            );

            -- Индексы для ускорения поиска
            CREATE INDEX IF NOT EXISTS IX_Messages_ChatId ON Messages(ChatId);
            CREATE INDEX IF NOT EXISTS IX_Messages_GroupId ON Messages(GroupId);
            CREATE INDEX IF NOT EXISTS IX_Messages_SenderId ON Messages(SenderId);
            CREATE INDEX IF NOT EXISTS IX_GroupMembers_UserId ON GroupMembers(UserId);
            CREATE INDEX IF NOT EXISTS IX_Contacts_UserId ON Contacts(UserId);
        ";
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
    }
}
