using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Media.Imaging;
using System.IO;
using SkiaSharp;
using S1Radar.Rendering;
using S1Radar.ViewModels;

namespace S1Radar.Views;

public partial class MainWindow : Window
{
    readonly MainViewModel vm = new();
    public MainWindow() { InitializeComponent(); DataContext=vm; }

    async void OpenClick(object? sender,RoutedEventArgs e)
    {
        var files=await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title="Open Source 1 VMF", AllowMultiple=false,
            FileTypeFilter=[new("Valve Map Format"){Patterns=["*.vmf"]},new("All files"){Patterns=["*.*"]}]
        });
        if(files.Count==0)return;
        LoadPath(files[0].Path.LocalPath);
    }

    void AnalyzeClick(object? sender,RoutedEventArgs e)
    {
        if(string.IsNullOrWhiteSpace(vm.FilePath)) return;
        vm.Analyze();
        SyncUi();
    }

    async void ExportClick(object? sender,RoutedEventArgs e)
    {
        if(vm.Scene is null)return;
        var folder=await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions{Title="Select export folder"});
        if(folder.Count==0)return;
        vm.Export(folder[0].Path.LocalPath,Resolution.SelectedIndex switch{0=>512,2=>2048,_=>1024},ShowMarkers.IsChecked==true);
        Status.Text=vm.Status;
    }

    void LoadPath(string path)
    {
        vm.SetFile(path); FileLabel.Text=vm.FilePath; vm.Analyze(); SyncUi();
    }

    void SyncUi()
    {
        FileLabel.Text=vm.FilePath; Status.Text=vm.Status; Stats.Text=vm.Stats; LevelsList.ItemsSource=vm.Levels;
        if(vm.Scene is null)return;
        using var bmp=RadarRenderer.Render(vm.Scene,1024,vm.Style,ShowMarkers.IsChecked!=false);
        using var data=bmp.Encode(SKEncodedImageFormat.Png,100); using var ms=new MemoryStream(data.ToArray()); Preview.Source=new Bitmap(ms);
    }
}
