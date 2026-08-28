namespace SecureLinkServer.Config;

/// <summary>
/// Конфигурация сервера
/// </summary>
public class ServerConfig
{
    /// <summary>
    /// Порт для WebSocket подключений
    /// </summary>
    public int Port { get; set; } = 8080;

    /// <summary>
    /// Путь к базе данных SQLite
    /// </summary>
    public string DatabasePath { get; set; } = "data/securelink.db";

    /// <summary>
    /// Путь для хранения файлов
    /// </summary>
    public string FilesPath { get; set; } = "data/files";

    /// <summary>
    /// Максимальный размер файла (в байтах)
    /// </summary>
    public long MaxFileSize { get; set; } = 100 * 1024 * 1024; // 100 MB

    /// <summary>
    /// Уровень логирования
    /// </summary>
    public string LogLevel { get; set; } = "Information";

    /// <summary>
    /// Хост для прослушивания
    /// </summary>
    public string Host { get; set; } = "*";

    /// <summary>
    /// Таймаут соединения (минуты)
    /// </summary>
    public int ConnectionTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// Включить SSL
    /// </summary>
    public bool UseSsl { get; set; } = false;

    /// <summary>
    /// Путь к SSL сертификату
    /// </summary>
    public string? SslCertificatePath { get; set; }

    /// <summary>
    /// Пароль SSL сертификата
    /// </summary>
    public string? SslCertificatePassword { get; set; }
}
