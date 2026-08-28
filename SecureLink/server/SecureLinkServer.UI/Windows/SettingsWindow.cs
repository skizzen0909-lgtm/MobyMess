using System.Windows;
using SecureLinkServer.Config;

namespace SecureLinkServer.UI;

/// <summary>
/// Окно настроек сервера
/// </summary>
public class SettingsWindow : Window
{
    private readonly ServerConfig _config;
    private System.Windows.Controls.TextBox? _portTextBox;
    private System.Windows.Controls.TextBox? _dbPathTextBox;
    private System.Windows.Controls.TextBox? _filesPathTextBox;
    private System.Windows.Controls.TextBox? _hostTextBox;
    private System.Windows.Controls.CheckBox? _sslCheckBox;

    public SettingsWindow(ServerConfig config)
    {
        _config = config;
        
        Title = "Настройки сервера - SecureLink";
        Width = 500;
        Height = 450;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = Application.Current.MainWindow;

        InitializeUi();
    }

    private void InitializeUi()
    {
        var mainGrid = new System.Windows.Controls.Grid();
        mainGrid.Margin = new Thickness(15);
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Порт
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Хост
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // БД
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Файлы
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // SSL
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Кнопки

        int row = 0;

        // Порт
        mainGrid.Children.Add(CreateLabel("Порт сервера:", row));
        _portTextBox = CreateTextBox(_config.Port.ToString(), row + 1);
        mainGrid.Children.Add(_portTextBox);
        row += 2;

        // Хост
        mainGrid.Children.Add(CreateLabel("Хост для прослушивания:", row));
        _hostTextBox = CreateTextBox(_config.Host, row + 1);
        mainGrid.Children.Add(_hostTextBox);
        row += 2;

        // Путь к БД
        mainGrid.Children.Add(CreateLabel("Путь к базе данных:", row));
        _dbPathTextBox = CreateTextBox(_config.DatabasePath, row + 1);
        mainGrid.Children.Add(_dbPathTextBox);
        row += 2;

        // Путь к файлам
        mainGrid.Children.Add(CreateLabel("Путь для хранения файлов:", row));
        _filesPathTextBox = CreateTextBox(_config.FilesPath, row + 1);
        mainGrid.Children.Add(_filesPathTextBox);
        row += 2;

        // SSL чекбокс
        var sslPanel = new System.Windows.Controls.StackPanel { Orientation = Orientation.Horizontal };
        _sslCheckBox = new System.Windows.Controls.CheckBox
        {
            Content = "Использовать SSL",
            IsChecked = _config.UseSsl,
            Margin = new Thickness(0, 5, 0, 10)
        };
        sslPanel.Children.Add(_sslCheckBox);
        System.Windows.Controls.Grid.SetRow(sslPanel, row);
        mainGrid.Children.Add(sslPanel);
        row++;

        // Кнопки
        var buttonPanel = new System.Windows.Controls.StackPanel 
        { 
            Orientation = Orientation.Horizontal, 
            HorizontalAlignment = HorizontalAlignment.Right 
        };

        var saveButton = new System.Windows.Controls.Button
        {
            Content = "Сохранить",
            Width = 100,
            Height = 35,
            Margin = new Thickness(5),
            IsDefault = true
        };
        saveButton.Click += SaveButton_Click;
        buttonPanel.Children.Add(saveButton);

        var cancelButton = new System.Windows.Controls.Button
        {
            Content = "Отмена",
            Width = 100,
            Height = 35,
            Margin = new Thickness(5),
            IsCancel = true
        };
        cancelButton.Click += (s, e) => DialogResult = false;
        buttonPanel.Children.Add(cancelButton);

        System.Windows.Controls.Grid.SetRow(buttonPanel, row);
        mainGrid.Children.Add(buttonPanel);

        Content = mainGrid;
    }

    private System.Windows.Controls.Label CreateLabel(string text, int row)
    {
        return new System.Windows.Controls.Label
        {
            Content = text,
            Target = null,
            Margin = new Thickness(0, 0, 0, 5)
        }
        {
            [System.Windows.Controls.Grid.RowProperty] = row
        };
    }

    private System.Windows.Controls.TextBox CreateTextBox(string text, int row)
    {
        var textBox = new System.Windows.Controls.TextBox
        {
            Text = text,
            Margin = new Thickness(0, 0, 0, 15),
            Padding = new Thickness(5)
        }
        {
            [System.Windows.Controls.Grid.RowProperty] = row
        };
        return textBox;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Сохраняем настройки
            if (int.TryParse(_portTextBox?.Text, out var port))
            {
                _config.Port = port;
            }

            _config.Host = _hostTextBox?.Text ?? "*";
            _config.DatabasePath = _dbPathTextBox?.Text ?? "data/securelink.db";
            _config.FilesPath = _filesPathTextBox?.Text ?? "data/files";
            _config.UseSsl = _sslCheckBox?.IsChecked ?? false;

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при сохранении настроек: {ex.Message}", 
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
