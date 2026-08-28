using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SecureLinkServer.Core.Interfaces;
using SecureLinkServer.Core.Models;

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
            // Проверяем существует ли пользователь
            var user = await _repository.GetUserByPhoneAsync(request.PhoneNumber);

            if (user == null)
            {
                // Создаем нового пользователя
                user = new User
                {
                    PhoneNumber = request.PhoneNumber,
                    DisplayName = request.PhoneNumber, // По умолчанию номер телефона
                    IsActive = true
                };
                await _repository.CreateUserAsync(user);
                _logger.LogInformation("New user registered: {PhoneNumber}", request.PhoneNumber);
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
