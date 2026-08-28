using System.IO;
using Microsoft.Extensions.Logging;
using SecureLinkServer.Core.Interfaces;

namespace SecureLinkServer.Core.Services;

/// <summary>
/// Сервис для хранения файлов
/// </summary>
public class FileStorageService : IFileStorageService
{
    private readonly string _basePath;
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(string basePath, ILogger<FileStorageService> logger)
    {
        _basePath = basePath;
        _logger = logger;

        // Создаем директорию если не существует
        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
        }
    }

    public Task<string> SaveFileAsync(byte[] data, string fileName, string mimeType, string userId)
    {
        // Создаем подпапку для пользователя
        var userDir = Path.Combine(_basePath, userId);
        if (!Directory.Exists(userDir))
        {
            Directory.CreateDirectory(userDir);
        }

        // Генерируем уникальное имя файла
        var extension = Path.GetExtension(fileName);
        var uniqueFileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(userDir, uniqueFileName);

        File.WriteAllBytes(filePath, data);

        _logger.LogInformation("File saved: {FilePath} ({Size} bytes)", filePath, data.Length);

        // Возвращаем относительный путь
        var relativePath = Path.Combine(userId, uniqueFileName);
        return Task.FromResult(relativePath);
    }

    public Task<byte[]> GetFileAsync(string filePath)
    {
        var fullPath = GetFilePath(filePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var data = File.ReadAllBytes(fullPath);
        return Task.FromResult(data);
    }

    public Task<bool> DeleteFileAsync(string filePath)
    {
        var fullPath = GetFilePath(filePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation("File deleted: {FilePath}", filePath);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public async Task<string> GenerateThumbnailAsync(string filePath, string outputDir)
    {
        // TODO: Реализовать генерацию превью для изображений и видео
        // Для пока просто возвращаем оригинальный путь
        await Task.Yield();
        return filePath;
    }

    public string GetFilePath(string relativePath)
    {
        return Path.Combine(_basePath, relativePath);
    }
}
