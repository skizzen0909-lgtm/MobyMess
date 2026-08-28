using System.Windows;
using SecureLink.Server.Core.Data;
using SecureLink.Server.Core.Models;

namespace SecureLink.Server.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Инициализация БД при старте (создание файла если нет)
        var settings = new ServerSettings();
        var dbContext = new AppDbContext(settings.DatabasePath);
        dbContext.Database.EnsureCreated();
        
        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}
