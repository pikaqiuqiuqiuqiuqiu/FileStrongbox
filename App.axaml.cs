using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FileStrongbox.Services;
using FileStrongbox.ViewModels;
using FileStrongbox.Views;

namespace FileStrongbox;

public partial class App : Application
{
    public static ICryptoService CryptoService { get; } = new CryptoService();
    public static IFileService FileService { get; } = new FileService(CryptoService);
    public static SettingsService SettingsService { get; } = new SettingsService();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
