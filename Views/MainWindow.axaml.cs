using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FileStrongbox.ViewModels;

namespace FileStrongbox.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var assets = Avalonia.Platform.AssetLoader.Open(new Uri("avares://FileStrongbox/Assets/icon.ico"));
        Icon = new WindowIcon(assets);

        var encryptZone = this.FindControl<Border>("EncryptZone");
        var decryptZone = this.FindControl<Border>("DecryptZone");

        if (encryptZone != null)
        {
            encryptZone.AddHandler(DragDrop.DropEvent, OnEncryptDrop);
            encryptZone.AddHandler(DragDrop.DragOverEvent, OnDragOver);
            encryptZone.AddHandler(DragDrop.DragEnterEvent, (s, e) => OnDragEnter(encryptZone, true));
            encryptZone.AddHandler(DragDrop.DragLeaveEvent, (s, e) => OnDragLeave(encryptZone, true));
        }

        if (decryptZone != null)
        {
            decryptZone.AddHandler(DragDrop.DropEvent, OnDecryptDrop);
            decryptZone.AddHandler(DragDrop.DragOverEvent, OnDragOver);
            decryptZone.AddHandler(DragDrop.DragEnterEvent, (s, e) => OnDragEnter(decryptZone, false));
            decryptZone.AddHandler(DragDrop.DragLeaveEvent, (s, e) => OnDragLeave(decryptZone, false));
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void OnDragEnter(Border zone, bool isEncrypt)
    {
        zone.BorderThickness = new Avalonia.Thickness(3);
        zone.Opacity = 0.8;
    }

    private void OnDragLeave(Border zone, bool isEncrypt)
    {
        zone.BorderThickness = new Avalonia.Thickness(2);
        zone.Opacity = 1;
    }

    private async void OnEncryptDrop(object? sender, DragEventArgs e)
    {
        var encryptZone = this.FindControl<Border>("EncryptZone");
        if (encryptZone != null) OnDragLeave(encryptZone, true);

        var files = e.Data.GetFiles();
        if (files != null && DataContext is MainWindowViewModel vm)
        {
            await vm.HandleEncryptDropAsync(files, this);
        }
    }

    private async void OnDecryptDrop(object? sender, DragEventArgs e)
    {
        var decryptZone = this.FindControl<Border>("DecryptZone");
        if (decryptZone != null) OnDragLeave(decryptZone, false);

        var files = e.Data.GetFiles();
        if (files != null && DataContext is MainWindowViewModel vm)
        {
            await vm.HandleDecryptDropAsync(files, this);
        }
    }
}
