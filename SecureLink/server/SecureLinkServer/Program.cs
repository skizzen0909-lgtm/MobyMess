using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SecureLinkServer.Config;
using SecureLinkServer.Core.Interfaces;
using SecureLinkServer.Core.Services;
using SecureLinkServer.Data.Entities;
using SecureLinkServer.Data.Repositories;
using SecureLinkServer.Services;
using SecureLinkServer.UI;

namespace SecureLinkServer;

/// <summary>
/// Точка входа приложения SecureLink Server
/// </summary>
public class App : Application
{
    private readonly IServiceProvider _serviceProvider;
    private WebSocketServerService? _webSocketServer;
    private CancellationTokenSource? _appCts;

    public App(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // Загружаем конфигурацию
            var config = LoadConfig();

            // Инициализируем сервисы
            var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
            logger.LogInformation("SecureLink Server starting...");

            // Показываем главное окно
            var mainWindow = new MainWindow(
                _serviceProvider.GetRequiredService<ILogger<MainWindow>>(),
                config);
            mainWindow.Show();

            // Инициализируем сервер но не запускаем
            InitializeServer(config);

            logger.LogInformation("SecureLink Server UI initialized");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при запуске: {ex.Message}", 
                "SecureLink Server", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private ServerConfig LoadConfig()
    {
        // TODO: Загрузить из JSON файла
        return new ServerConfig
        {
            Port = 8080,
            Host = "*",
            DatabasePath = "data/securelink.db",
            FilesPath = "data/files",
            MaxFileSize = 100 * 1024 * 1024,
            LogLevel = "Information"
        };
    }

    private void InitializeServer(ServerConfig config)
    {
        var logger = _serviceProvider.GetRequiredService<ILogger<WebSocketServerService>>();

        _webSocketServer = new WebSocketServerService(
            config,
            _serviceProvider.GetRequiredService<IConnectionManager>(),
            _serviceProvider.GetRequiredService<IMessageHandler>(),
            _serviceProvider.GetRequiredService<IAuthService>(),
            logger);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);

        // Останавливаем сервер
        if (_webSocketServer != null)
        {
            await _webSocketServer.StopAsync();
        }

        _appCts?.Cancel();
        _appCts?.Dispose();
    }
}

/// <summary>
/// Программная точка входа
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main()
    {
        var services = ConfigureServices();
        var app = new App(services.BuildServiceProvider());
        app.Run();
    }

    private static IServiceCollection ConfigureServices()
    {
        var services = new ServiceCollection();

        // Логирование
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddConsole();
        });

        // Конфигурация
        services.AddSingleton<ServerConfig>();

        // База данных
        services.AddSingleton<DatabaseContext>(sp =>
        {
            var config = sp.GetRequiredService<ServerConfig>();
            
            // Создаем директорию для БД если не существует
            var dbDir = System.IO.Path.GetDirectoryName(config.DatabasePath);
            if (!string.IsNullOrEmpty(dbDir) && !System.IO.Directory.Exists(dbDir))
            {
                System.IO.Directory.CreateDirectory(dbDir);
            }
            
            return new DatabaseContext(config.DatabasePath);
        });
        services.AddSingleton<IDataRepository, DataRepository>();

        // Хранилище файлов
        services.AddSingleton<IFileStorageService>(sp =>
        {
            var config = sp.GetRequiredService<ServerConfig>();
            var logger = sp.GetRequiredService<ILogger<FileStorageService>>();
            
            // Создаем директорию для файлов если не существует
            if (!System.IO.Directory.Exists(config.FilesPath))
            {
                System.IO.Directory.CreateDirectory(config.FilesPath);
            }
            
            return new FileStorageService(config.FilesPath, logger);
        });

        // Менеджер подключений
        services.AddSingleton<IConnectionManager, ConnectionManager>();

        // Аутентификация
        services.AddSingleton<IAuthService, AuthService>();

        // Обработчик сообщений
        services.AddSingleton<IMessageHandler, MessageHandler>();

        // UI
        services.AddSingleton<MainWindow>();

        return services;
    }
}
