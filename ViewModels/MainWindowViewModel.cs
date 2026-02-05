using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileStrongbox.Models;
using FileStrongbox.Services;
using FileStrongbox.Views;

namespace FileStrongbox.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public static readonly IValueConverter DisabledBgConverter = new DisabledBackgroundConverter();
    public static readonly IValueConverter DisabledBorderConverter = new DisabledBorderBrushConverter();
    public static readonly IValueConverter DisabledOpacityConverter = new DisabledOpacityValueConverter();

    [ObservableProperty]
    private string _statusMessage = "就绪";

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _progressText = "";

    [ObservableProperty]
    private string _currentFilePath = "";

    [ObservableProperty]
    private string _currentOperation = "";

    private Window? _parentWindow;

    public ObservableCollection<string> ProcessingLog { get; } = new();

    public async Task HandleEncryptDropAsync(IEnumerable<IStorageItem> items, Window parentWindow)
    {
        if (IsProcessing) return;
        _parentWindow = parentWindow;

        var paths = items
            .Select(i => i.TryGetLocalPath())
            .Where(p => p != null)
            .Cast<string>()
            .ToList();

        if (paths.Count == 0) return;

        var dialog = new PasswordDialog(isEncryption: true);
        var password = await dialog.ShowDialog<string?>(parentWindow);

        if (string.IsNullOrEmpty(password)) return;

        await ProcessFilesAsync(paths, password, isEncrypt: true);
    }

    public async Task HandleDecryptDropAsync(IEnumerable<IStorageItem> items, Window parentWindow)
    {
        if (IsProcessing) return;
        _parentWindow = parentWindow;

        var paths = items
            .Select(i => i.TryGetLocalPath())
            .Where(p => p != null)
            .Cast<string>()
            .ToList();

        if (paths.Count == 0) return;

        var dialog = new PasswordDialog(isEncryption: false);
        var password = await dialog.ShowDialog<string?>(parentWindow);

        if (string.IsNullOrEmpty(password)) return;

        await ProcessFilesAsync(paths, password, isEncrypt: false);
    }

    private async Task ProcessFilesAsync(List<string> paths, string password, bool isEncrypt)
    {
        IsProcessing = true;
        Progress = 0;
        ProgressText = "0%";
        ProcessingLog.Clear();

        var operation = isEncrypt ? "加密中" : "解密中";
        CurrentOperation = operation;

        var progressReporter = new Progress<ProgressInfo>(info =>
        {
            Progress = info.Percentage;
            ProgressText = $"{info.ProcessedFiles}/{info.TotalFiles}";
            CurrentFilePath = info.CurrentFile;
            StatusMessage = $"{operation}：{TruncatePath(info.CurrentFile, 50)}";
        });

        int totalProcessed = 0;
        int totalFailed = 0;

        try
        {
            var settings = App.SettingsService.Settings;

            foreach (var path in paths)
            {
                OperationResult result;
                if (isEncrypt)
                {
                    result = await App.FileService.EncryptAsync(path, password, settings.FileNameFormat, settings.CustomExtension, progressReporter);
                }
                else
                {
                    result = await App.FileService.DecryptAsync(path, password, progressReporter);
                }

                totalProcessed += result.ProcessedFiles;
                totalFailed += result.FailedFiles;
                ProcessingLog.Add(result.Message);
            }
        }
        catch (Exception ex)
        {
            ProcessingLog.Add($"错误：{ex.Message}");
        }
        finally
        {
            IsProcessing = false;
            Progress = 0;
            ProgressText = "";
            CurrentFilePath = "";
            CurrentOperation = "";

            await ShowCompletionDialog(isEncrypt, totalProcessed, totalFailed);
        }
    }

    private async Task ShowCompletionDialog(bool isEncrypt, int processed, int failed)
    {
        var operationName = isEncrypt ? "加密" : "解密";
        string message;
        string title;

        if (failed == 0 && processed > 0)
        {
            title = $"{operationName}完成";
            message = $"成功{operationName} {processed} 个文件";
            StatusMessage = message;
        }
        else if (processed == 0 && failed > 0)
        {
            title = $"{operationName}失败";
            message = isEncrypt
                ? $"加密失败，共 {failed} 个文件"
                : $"解密失败，可能是密码错误或文件未加密";
            StatusMessage = message;
        }
        else if (processed > 0 && failed > 0)
        {
            title = $"{operationName}完成";
            message = $"{operationName}完成：{processed} 个成功，{failed} 个失败";
            StatusMessage = message;
        }
        else
        {
            title = "提示";
            message = "没有找到可处理的文件";
            StatusMessage = "就绪";
        }

        if (_parentWindow != null)
        {
            await ShowMessageDialog(title, message);
        }
    }

    private async Task ShowMessageDialog(string title, string message)
    {
        if (_parentWindow == null) return;

        var okButton = new Button
        {
            Content = "确定",
            Width = 80,
            HorizontalAlignment = HorizontalAlignment.Right,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };

        var footer = new Border
        {
            Background = Brush.Parse("#f8f9fa"),
            BorderBrush = Brush.Parse("#dee2e6"),
            BorderThickness = new Avalonia.Thickness(0, 1, 0, 0),
            Padding = new Avalonia.Thickness(20, 12),
            Child = okButton
        };

        var content = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(20),
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = Avalonia.Media.TextAlignment.Center
        };

        var panel = new DockPanel();
        DockPanel.SetDock(footer, Dock.Bottom);
        panel.Children.Add(footer);
        panel.Children.Add(content);

        var dialog = new Window
        {
            Title = title,
            Width = 350,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false,
            Icon = null,
            Content = panel
        };

        okButton.Click += (s, e) => dialog.Close();
        await dialog.ShowDialog(_parentWindow);
    }

    private static string TruncatePath(string path, int maxLength)
    {
        if (string.IsNullOrEmpty(path) || path.Length <= maxLength)
            return path;

        var fileName = Path.GetFileName(path);
        var directory = Path.GetDirectoryName(path) ?? "";

        if (fileName.Length >= maxLength - 3)
        {
            return "..." + fileName.Substring(fileName.Length - maxLength + 3);
        }

        var availableForDir = maxLength - fileName.Length - 4;

        if (availableForDir <= 0)
        {
            return "...\\" + fileName;
        }

        var root = Path.GetPathRoot(path) ?? "";
        if (root.Length > 0 && availableForDir > root.Length + 3)
        {
            var remaining = availableForDir - root.Length - 3;
            var dirWithoutRoot = directory.Substring(root.Length);
            if (dirWithoutRoot.Length > remaining)
            {
                return root + "..." + dirWithoutRoot.Substring(dirWithoutRoot.Length - remaining) + "\\" + fileName;
            }
        }

        return directory.Substring(0, availableForDir) + "...\\" + fileName;
    }

    [RelayCommand]
    private async Task OpenSettingsAsync(Window parentWindow)
    {
        var settingsWindow = new SettingsWindow();
        await settingsWindow.ShowDialog(parentWindow);
    }

    [RelayCommand]
    private void OpenSourceCode()
    {
        var url = "https://github.com/pikaqiuqiuqiuqiuqiu/FileStrongbox";
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    [RelayCommand]
    private async Task ShowAboutAsync(Window parentWindow)
    {
        var aboutWindow = new AboutWindow();
        await aboutWindow.ShowDialog(parentWindow);
    }
}

public class DisabledBackgroundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isDisabled = value is bool b && b;
        var normalColor = parameter?.ToString() ?? "#ffffff";
        return isDisabled ? Brush.Parse("#e0e0e0") : Brush.Parse(normalColor);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class DisabledBorderBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isDisabled = value is bool b && b;
        var normalColor = parameter?.ToString() ?? "#cccccc";
        return isDisabled ? Brush.Parse("#bdbdbd") : Brush.Parse(normalColor);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class DisabledOpacityValueConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isDisabled = value is bool b && b;
        return isDisabled ? 0.5 : 1.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
