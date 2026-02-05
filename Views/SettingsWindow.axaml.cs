using Avalonia.Controls;
using Avalonia.Interactivity;
using FileStrongbox.Models;

namespace FileStrongbox.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        SetupEvents();
        LoadSettings();
    }

    private void SetupEvents()
    {
        var fullEncrypt = this.FindControl<RadioButton>("FullEncryptOption");
        var keepOriginal = this.FindControl<RadioButton>("KeepOriginalOption");
        var newExtension = this.FindControl<RadioButton>("NewExtensionOption");

        if (fullEncrypt != null)
            fullEncrypt.IsCheckedChanged += OnRadioButtonChanged;
        if (keepOriginal != null)
            keepOriginal.IsCheckedChanged += OnRadioButtonChanged;
        if (newExtension != null)
            newExtension.IsCheckedChanged += OnRadioButtonChanged;
    }

    private void OnRadioButtonChanged(object? sender, RoutedEventArgs e)
    {
        UpdateCustomExtensionVisibility();
    }

    private void UpdateCustomExtensionVisibility()
    {
        var newExtension = this.FindControl<RadioButton>("NewExtensionOption");
        var customExtBox = this.FindControl<TextBox>("CustomExtensionBox");

        if (customExtBox != null && newExtension != null)
        {
            customExtBox.IsVisible = newExtension.IsChecked == true;
        }
    }

    private void LoadSettings()
    {
        var settings = App.SettingsService.Settings;

        var fullEncrypt = this.FindControl<RadioButton>("FullEncryptOption");
        var keepOriginal = this.FindControl<RadioButton>("KeepOriginalOption");
        var newExtension = this.FindControl<RadioButton>("NewExtensionOption");
        var customExtBox = this.FindControl<TextBox>("CustomExtensionBox");

        switch (settings.FileNameFormat)
        {
            case FileNameFormat.FullEncrypt:
                if (fullEncrypt != null) fullEncrypt.IsChecked = true;
                break;
            case FileNameFormat.KeepOriginal:
                if (keepOriginal != null) keepOriginal.IsChecked = true;
                break;
            case FileNameFormat.NewExtension:
                if (newExtension != null) newExtension.IsChecked = true;
                break;
        }

        if (customExtBox != null)
        {
            customExtBox.Text = settings.CustomExtension;
        }

        UpdateCustomExtensionVisibility();
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        var fullEncrypt = this.FindControl<RadioButton>("FullEncryptOption");
        var keepOriginal = this.FindControl<RadioButton>("KeepOriginalOption");
        var newExtension = this.FindControl<RadioButton>("NewExtensionOption");
        var customExtBox = this.FindControl<TextBox>("CustomExtensionBox");

        FileNameFormat format = FileNameFormat.FullEncrypt;

        if (fullEncrypt?.IsChecked == true)
            format = FileNameFormat.FullEncrypt;
        else if (keepOriginal?.IsChecked == true)
            format = FileNameFormat.KeepOriginal;
        else if (newExtension?.IsChecked == true)
            format = FileNameFormat.NewExtension;

        var customExt = customExtBox?.Text ?? ".data";
        if (string.IsNullOrWhiteSpace(customExt))
        {
            customExt = ".data";
        }
        if (!customExt.StartsWith("."))
        {
            customExt = "." + customExt;
        }

        App.SettingsService.Settings.FileNameFormat = format;
        App.SettingsService.Settings.CustomExtension = customExt;
        App.SettingsService.Save();

        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
