using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OsdDemo.ViewModels;
using OsdDemo.Windows;

namespace OsdDemo;
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _dataContext;
    private readonly VideoWindow _videoWindow;
    public MainWindow()
    {
        InitializeComponent();
        var videoWindowViewModel = new VideoWindowViewModel();
        _dataContext = new MainWindowViewModel(videoWindowViewModel);
        _videoWindow = new VideoWindow
        {
            DataContext = _dataContext.VideoWindowViewModel
        };
        _videoWindow.Loaded += VideoWindowOnLoaded;
        DataContext = _dataContext;
        PositionChanged += OnPositionChanged;
        SizeChanged += OnSizeChanged;
    }

    private void VideoWindowOnLoaded(object? sender, RoutedEventArgs e)
    {
        Activate();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        _videoWindow.Width = Width;
        _videoWindow.Height = Height;
    }

    private void OnPositionChanged(object? sender, PixelPointEventArgs e)
    {
        _videoWindow.Position = e.Point;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        _videoWindow.Show();
        Activate();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _videoWindow.Close();
    }
}