using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SecureLinkServer.Core.Interfaces;
using SecureLinkServer.Core.Models;
using SecureLinkServer.Security;

namespace SecureLinkServer.Core.Services;

/// <summary>
/// Сервис аутентификации
/// </summary>
public class AuthService : IAuthService
{
    private readonly IDataRepository _repository;
    private readonly ILogger<AuthService> _logger;
    private readonly Dictionary<string, string> _tokens = new(); // В памяти для простоты, в продакшене использовать Redis

    public AuthService(IDataRepository repository, ILogger<AuthService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<AuthResponse> AuthenticateAsync(AuthRequest request)
    {
        try
        {
            // Валидация номера телефона
            if (!SecurityValidator.IsValidPhone(request.PhoneNumber))
            {
                _logger.LogWarning("Invalid phone number format: {PhoneNumber}", request.PhoneNumber);
                return new AuthResponse
                {
                    Success = false,
                    ErrorMessage = "Invalid phone number format"
                };
            }

            // Нормализуем номер телефона
            var normalizedPhone = SecurityValidator.NormalizePhone(request.PhoneNumber);

            // Проверяем существует ли пользователь
            var user = await _repository.GetUserByPhoneAsync(normalizedPhone);

            if (user == null)
            {
                // Создаем нового пользователя
                user = new User
                {
                    PhoneNumber = normalizedPhone,
                    DisplayName = normalizedPhone, // По умолчанию номер телефона
                    IsActive = true
                };
                await _repository.CreateUserAsync(user);
                _logger.LogInformation("New user registered: {PhoneNumber}", normalizedPhone);
            }

            // Генерируем токен
            var token = GenerateToken(user.Id);
            _tokens[token] = user.Id;

            _logger.LogInformation("User authenticated: {UserId}", user.Id);

            return new AuthResponse
            {
                Success = true,
                UserId = user.Id,
                Token = token
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication failed for {PhoneNumber}", request.PhoneNumber);
            return new AuthResponse
            {
                Success = false,
                ErrorMessage = "Authentication failed"
            };
        }
    }

    public Task<bool> ValidateTokenAsync(string token, out string? userId)
    {
        if (_tokens.TryGetValue(token, out var id))
        {
            userId = id;
            return Task.FromResult(true);
        }
        userId = null;
        return Task.FromResult(false);
    }

    public Task<string> GenerateTokenAsync(string userId)
    {
        var token = GenerateToken(userId);
        _tokens[token] = userId;
        return Task.FromResult(token);
    }

    private string GenerateToken(string userId)
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);

        var base64 = Convert.ToBase64String(randomBytes);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        return $"{userId}.{base64}.{timestamp}";
    }
}
