using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Diagnostics;

namespace FileStrongbox.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    private void OnLink52PojieClick(object? sender, RoutedEventArgs e)
    {
        OpenUrl("https://www.52pojie.cn/");
    }

    private void OnLinkGitHubClick(object? sender, RoutedEventArgs e)
    {
        OpenUrl("https://github.com/pikaqiuqiuqiuqiuqiu/FileStrongbox");
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }
}
