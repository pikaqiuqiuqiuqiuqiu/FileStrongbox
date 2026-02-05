using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace FileStrongbox.Views;

public partial class PasswordDialog : Window
{
    private readonly bool _isEncryption;

    public PasswordDialog() : this(true) { }

    public PasswordDialog(bool isEncryption)
    {
        InitializeComponent();
        _isEncryption = isEncryption;

        var confirmLabel = this.FindControl<TextBlock>("ConfirmLabel");
        var confirmBox = this.FindControl<TextBox>("ConfirmPasswordBox");

        if (!isEncryption)
        {
            if (confirmLabel != null) confirmLabel.IsVisible = false;
            if (confirmBox != null) confirmBox.IsVisible = false;
            Title = "输入密码";
        }
        else
        {
            Title = "设置加密密码";
        }
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        var passwordBox = this.FindControl<TextBox>("PasswordBox");
        var confirmBox = this.FindControl<TextBox>("ConfirmPasswordBox");

        var password = passwordBox?.Text ?? "";
        var confirm = confirmBox?.Text ?? "";

        if (string.IsNullOrEmpty(password))
        {
            ShowError("请输入密码");
            return;
        }

        if (_isEncryption)
        {
            if (password != confirm)
            {
                ShowError("两次输入的密码不一致");
                return;
            }

            if (password.Length < 4)
            {
                ShowError("密码长度至少需要4个字符");
                return;
            }
        }

        Close(password);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private async void ShowError(string message)
    {
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
            Margin = new Avalonia.Thickness(20)
        };

        var panel = new DockPanel();
        DockPanel.SetDock(footer, Dock.Bottom);
        panel.Children.Add(footer);
        panel.Children.Add(content);

        var dialog = new Window
        {
            Title = "提示",
            Width = 300,
            Height = 130,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false,
            Content = panel
        };

        okButton.Click += (s, e) => dialog.Close();
        await dialog.ShowDialog(this);
    }
}
