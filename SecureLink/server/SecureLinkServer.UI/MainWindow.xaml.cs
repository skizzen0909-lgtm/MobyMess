using System.Windows;
using Microsoft.Extensions.Logging;

namespace SecureLinkServer.UI;

/// <summary>
/// Главное окно приложения сервера
/// </summary>
public partial class MainWindow : Window
{
    private readonly ILogger<MainWindow> _logger;
    private readonly ServerConfig _config;

    public MainWindow(ILogger<MainWindow> logger, ServerConfig config)
    {
        _logger = logger;
        _config = config;
        
        InitializeComponent();
        InitializeUi();
    }

    private void InitializeComponent()
    {
        // Создаем основной интерфейс программно
        Width = 800;
        Height = 600;
        Title = "SecureLink Server";
    }

    private void InitializeUi()
    {
        var mainGrid = new System.Windows.Controls.Grid();
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Заголовок
        var headerText = new System.Windows.Controls.TextBlock
        {
            Text = "SecureLink Messenger Server",
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(10)
        };
        System.Windows.Controls.Grid.SetRow(headerText, 0);
        mainGrid.Children.Add(headerText);

        // Статус сервера
        var statusPanel = new System.Windows.Controls.StackPanel { Margin = new Thickness(10) };
        System.Windows.Controls.Grid.SetRow(statusPanel, 1);

        var statusText = new System.Windows.Controls.TextBlock
        {
            Name = "StatusText",
            Text = "Статус: Остановлен",
            FontSize = 16,
            Margin = new Thickness(0, 0, 0, 10)
        };
        statusPanel.Children.Add(statusText);

        var connectionsText = new System.Windows.Controls.TextBlock
        {
            Name = "ConnectionsText",
            Text = "Подключений: 0",
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 10)
        };
        statusPanel.Children.Add(connectionsText);

        var logBox = new System.Windows.Controls.TextBox
        {
            Name = "LogBox",
            IsReadOnly = true,
            AcceptsReturn = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 12
        };
        statusPanel.Children.Add(logBox);

        mainGrid.Children.Add(statusPanel);

        // Панель управления
        var controlPanel = new System.Windows.Controls.StackPanel 
        { 
            Orientation = Orientation.Horizontal, 
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(10)
        };
        System.Windows.Controls.Grid.SetRow(controlPanel, 2);

        var startButton = new System.Windows.Controls.Button
        {
            Content = "Запустить сервер",
            Width = 150,
            Height = 40,
            Margin = new Thickness(5),
            Name = "StartButton"
        };
        startButton.Click += (s, e) => StartServer();
        controlPanel.Children.Add(startButton);

        var stopButton = new System.Windows.Controls.Button
        {
            Content = "Остановить сервер",
            Width = 150,
            Height = 40,
            Margin = new Thickness(5),
            Name = "StopButton",
            IsEnabled = false
        };
        stopButton.Click += (s, e) => StopServer();
        controlPanel.Children.Add(stopButton);

        var settingsButton = new System.Windows.Controls.Button
        {
            Content = "Настройки",
            Width = 150,
            Height = 40,
            Margin = new Thickness(5)
        };
        settingsButton.Click += (s, e) => OpenSettings();
        controlPanel.Children.Add(settingsButton);

        mainGrid.Children.Add(controlPanel);

        Content = mainGrid;
    }

    private void StartServer()
    {
        // TODO: Вызвать запуск сервера
        _logger.LogInformation("Starting server...");
        MessageBox.Show("Сервер запускается...", "SecureLink Server", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void StopServer()
    {
        // TODO: Вызвать остановку сервера
        _logger.LogInformation("Stopping server...");
        MessageBox.Show("Сервер останавливается...", "SecureLink Server", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OpenSettings()
    {
        var settingsWindow = new SettingsWindow(_config);
        settingsWindow.ShowDialog();
    }
}
