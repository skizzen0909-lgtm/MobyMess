using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using SecureLink.Server.Core.Data;
using SecureLink.Server.Core.Models;
using SecureLink.Server.Core.Services;

namespace SecureLink.Server.Wpf;

public partial class MainWindow : Window
{
    private AppDbContext? _dbContext;
    private WebSocketServer? _server;
    private ServerSettings _settings = new();
    private bool _isRunning;

    public MainWindow()
    {
        InitializeComponent();
        LoadSettings();
        UpdateStatus();
    }

    private void LoadSettings()
    {
        var settingsPath = "settings.json";
        if (File.Exists(settingsPath))
        {
            var json = File.ReadAllText(settingsPath);
            _settings = System.Text.Json.JsonSerializer.Deserialize<ServerSettings>(json) ?? new ServerSettings();
        }

        TxtPort.Text = _settings.Port.ToString();
        TxtIp.Text = _settings.IpAddress;
        TxtDbPath.Text = _settings.DatabasePath;
        TxtFilesPath.Text = _settings.FilesPath;
        TxtMaxFileSize.Text = _settings.MaxFileSizeMb.ToString();
    }

    private void SaveSettings()
    {
        _settings.Port = int.Parse(TxtPort.Text);
        _settings.IpAddress = TxtIp.Text;
        _settings.DatabasePath = TxtDbPath.Text;
        _settings.FilesPath = TxtFilesPath.Text;
        _settings.MaxFileSizeMb = int.Parse(TxtMaxFileSize.Text);

        Directory.CreateDirectory(Path.GetDirectoryName(_settings.DatabasePath)!);
        Directory.CreateDirectory(_settings.FilesPath);

        var json = System.Text.Json.JsonSerializer.Serialize(_settings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText("settings.json", json);
    }

    private async void BtnStartStop_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            StopServer();
        }
        else
        {
            await StartServerAsync();
        }
    }

    private async Task StartServerAsync()
    {
        try
        {
            SaveSettings();
            _dbContext = new AppDbContext(_settings.DatabasePath);
            await _dbContext.Database.MigrateAsync();

            _server = new WebSocketServer(_dbContext, _settings);
            
            _ = Task.Run(async () =>
            {
                try
                {
                    await _server!.StartAsync();
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => LogMessage($"Ошибка сервера: {ex.Message}"));
                }
            });

            _isRunning = true;
            BtnStartStop.Content = "Остановить";
            LblStatus.Content = "Статус: Работает";
            LblStatus.Foreground = System.Windows.Media.Brushes.Green;
            LogMessage("Сервер запущен");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка запуска: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void StopServer()
    {
        _server?.Stop();
        _dbContext?.Dispose();
        _isRunning = false;
        BtnStartStop.Content = "Запустить";
        LblStatus.Content = "Статус: Остановлен";
        LblStatus.Foreground = System.Windows.Media.Brushes.Red;
        LogMessage("Сервер остановлен");
    }

    private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        MessageBox.Show("Настройки сохранены", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnClearDb_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Вы уверены? Все данные будут удалены!", "Подтверждение", 
            MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            try
            {
                if (_dbContext != null)
                {
                    _dbContext.Database.EnsureDeleted();
                    _dbContext.Database.Migrate();
                    LogMessage("База данных очищена");
                }
                else
                {
                    var dbPath = _settings.DatabasePath;
                    if (File.Exists(dbPath))
                        File.Delete(dbPath);
                    LogMessage("База данных удалена");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void LogMessage(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        TxtLog.AppendText($"[{timestamp}] {message}\n");
        TxtLog.ScrollToEnd();
    }

    private void UpdateStatus()
    {
        LblStatus.Content = "Статус: Остановлен";
        LblStatus.Foreground = System.Windows.Media.Brushes.Red;
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isRunning)
        {
            StopServer();
        }
    }
}
