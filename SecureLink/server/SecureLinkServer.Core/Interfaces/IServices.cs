using SecureLinkServer.Core.Models;

namespace SecureLinkServer.Core.Interfaces;

/// <summary>
/// Интерфейс для управления подключениями клиентов
/// </summary>
public interface IConnectionManager
{
    Task AddConnectionAsync(string userId, IClientConnection connection);
    Task RemoveConnectionAsync(string userId);
    IClientConnection? GetConnection(string userId);
    IEnumerable<string> GetConnectedUsers();
    Task SendMessageToUserAsync(string userId, MessagePacket packet);
    Task SendMessageToGroupAsync(string groupId, MessagePacket packet);
}

/// <summary>
/// Интерфейс клиентского подключения
/// </summary>
public interface IClientConnection
{
    string UserId { get; }
    string ConnectionId { get; }
    DateTime ConnectedAt { get; }
    Task SendAsync(MessagePacket packet);
    Task SendBinaryAsync(byte[] data, string fileName, MessageType type);
    void Disconnect();
}

/// <summary>
/// Интерфейс для работы с базой данных
/// </summary>
public interface IDataRepository
{
    // Пользователи
    Task<User?> GetUserByPhoneAsync(string phoneNumber);
    Task<User?> GetUserByIdAsync(string userId);
    Task<User> CreateUserAsync(User user);
    Task UpdateUserAsync(User user);
    
    // Чаты
    Task<Chat?> GetChatBetweenUsersAsync(string user1Id, string user2Id);
    Task<Chat> CreateChatAsync(Chat chat);
    Task<List<Chat>> GetUserChatsAsync(string userId);
    
    // Группы
    Task<Group?> GetGroupByIdAsync(string groupId);
    Task<Group> CreateGroupAsync(Group group);
    Task AddMemberToGroupAsync(string groupId, string userId);
    Task RemoveMemberFromGroupAsync(string groupId, string userId);
    Task<List<Group>> GetUserGroupsAsync(string userId);
    
    // Сообщения
    Task SaveMessageAsync(ChatMessage message);
    Task<List<ChatMessage>> GetChatMessagesAsync(string chatId, int count = 50);
    Task<List<ChatMessage>> GetGroupMessagesAsync(string groupId, int count = 50);
    Task MarkMessageAsDeliveredAsync(string messageId);
    Task MarkMessageAsReadAsync(string messageId);
    
    // Контакты
    Task<List<Contact>> GetContactsAsync(string userId);
    Task SyncContactsAsync(string userId, List<Contact> contacts);
    
    // Проверка прав доступа
    Task<bool> HasChatAccessAsync(string userId, string chatId);
    Task<bool> HasGroupAccessAsync(string userId, string groupId);
}

/// <summary>
/// Интерфейс для обработки файлов
/// </summary>
public interface IFileStorageService
{
    Task<string> SaveFileAsync(byte[] data, string fileName, string mimeType, string userId);
    Task<byte[]> GetFileAsync(string filePath);
    Task<bool> DeleteFileAsync(string filePath);
    Task<string> GenerateThumbnailAsync(string filePath, string outputDir);
    string GetFilePath(string relativePath);
}

/// <summary>
/// Интерфейс для обработки сообщений
/// </summary>
public interface IMessageHandler
{
    Task HandleMessageAsync(IClientConnection connection, MessagePacket packet);
    Task HandleBinaryDataAsync(IClientConnection connection, byte[] data, string fileName, MessageType type);
}

/// <summary>
/// Интерфейс для аутентификации
/// </summary>
public interface IAuthService
{
    Task<AuthResponse> AuthenticateAsync(AuthRequest request);
    Task<bool> ValidateTokenAsync(string token, out string? userId);
    Task<string> GenerateTokenAsync(string userId);
}
