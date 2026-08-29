using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Web;

namespace SecureLink.Server.Core.Security
{
    /// <summary>
    /// Класс для валидации входных данных и защиты от уязвимостей
    /// </summary>
    public static class SecurityValidator
    {
        // Максимальная длина сообщений
        private const int MaxMessageLength = 10000;
        private const int MaxNameLength = 100;
        private const int MaxPhoneLength = 20;
        
        // Разрешенные MIME-типы для файлов
        private static readonly string[] AllowedMimeTypes = {
            "image/jpeg", "image/png", "image/gif", "image/webp",
            "video/mp4", "video/3gpp", "video/quicktime",
            "audio/mp4", "audio/mpeg", "audio/ogg", "audio/wav",
            "application/pdf", "text/plain", 
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };

        // Максимальный размер файла (100 MB)
        private const long MaxFileSize = 100 * 1024 * 1024;

        /// <summary>
        /// Валидация UUID
        /// </summary>
        public static bool IsValidUuid(string uuid)
        {
            if (string.IsNullOrWhiteSpace(uuid))
                return false;

            return Guid.TryParse(uuid, out _);
        }

        /// <summary>
        /// Валидация номера телефона (только цифры, +, -, скобки)
        /// </summary>
        public static bool IsValidPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return false;

            if (phone.Length > MaxPhoneLength)
                return false;

            // Разрешаем только цифры, +, -, пробелы, скобки
            return Regex.IsMatch(phone, @"^[\d\s\-\+\(\)]+$");
        }

        /// <summary>
        /// Валидация имени пользователя
        /// </summary>
        public static bool IsValidName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            if (name.Length > MaxNameLength || name.Length < 2)
                return false;

            // Запрещаем HTML-теги и скрипты
            if (ContainsHtml(name))
                return false;

            return true;
        }

        /// <summary>
        /// Валидация содержимого сообщения
        /// </summary>
        public static bool IsValidMessage(string message, string type = "text")
        {
            if (string.IsNullOrEmpty(message) && type == "text")
                return false;

            if (message != null && message.Length > MaxMessageLength)
                return false;

            if (type == "text" && ContainsHtml(message))
                return false;

            return true;
        }

        /// <summary>
        /// Проверка на наличие HTML-тегов (защита от XSS)
        /// </summary>
        public static bool ContainsHtml(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            return Regex.IsMatch(input, @"<[^>]*>", RegexOptions.Compiled);
        }

        /// <summary>
        /// Санитизация строки (удаление потенциально опасных символов)
        /// </summary>
        public static string SanitizeInput(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Удаляем HTML-теги
            input = Regex.Replace(input, @"<[^>]*>", string.Empty);
            
            // Экранируем специальные символы
            input = HttpUtility.HtmlEncode(input);

            return input.Trim();
        }

        /// <summary>
        /// Защита от Path Traversal атак
        /// </summary>
        public static bool IsValidFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            // Запрещаем навигацию по директориям
            if (fileName.Contains("..") || fileName.Contains("/") || fileName.Contains("\\"))
                return false;

            // Запрещаем специальные символы
            if (Regex.IsMatch(fileName, @"[<>:""|?*]"))
                return false;

            return true;
        }

        /// <summary>
        /// Получение безопасного имени файла
        /// </summary>
        public static string GetSafeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return Guid.NewGuid().ToString();

            // Удаляем путь, если он есть
            fileName = Path.GetFileName(fileName);

            // Заменяем опасные символы
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }

            // Если имя пустое после очистки, генерируем новое
            if (string.IsNullOrWhiteSpace(fileName))
                return Guid.NewGuid().ToString() + ".dat";

            return fileName;
        }

        /// <summary>
        /// Проверка MIME-типа файла
        /// </summary>
        public static bool IsValidMimeType(string mimeType)
        {
            if (string.IsNullOrWhiteSpace(mimeType))
                return false;

            return Array.Exists(AllowedMimeTypes, item => item.Equals(mimeType, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Проверка размера файла
        /// </summary>
        public static bool IsValidFileSize(long fileSizeBytes)
        {
            return fileSizeBytes > 0 && fileSizeBytes <= MaxFileSize;
        }

        /// <summary>
        /// Нормализация телефонного номера (удаление лишних символов)
        /// </summary>
        public static string NormalizePhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return string.Empty;

            // Оставляем только цифры и +
            return Regex.Replace(phone, @"[^\d+]", "");
        }

        /// <summary>
        /// Хэширование телефонного номера для безопасного поиска
        /// Использует SHA256 для создания хэша
        /// </summary>
        public static string HashPhoneNumber(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return string.Empty;

            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(phone));
            return BitConverter.ToString(hashedBytes).Replace("-", "").ToLowerInvariant();
        }
    }
}
