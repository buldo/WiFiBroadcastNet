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
    private readonly WindowsPlaybackModule _playbackModule;

    public MainWindow()
    {
        InitializeComponent();

        _playbackModule = new WindowsPlaybackModule();

        _playbackModule.VideoWindow.Loaded += VideoWindowOnLoaded;
        _dataContext = new MainWindowViewModel(_playbackModule);
        DataContext = _dataContext;
        PositionChanged += OnPositionChanged;
        SizeChanged += OnSizeChanged;
    }

    private void VideoWindowOnLoaded(object sender, System.Windows.RoutedEventArgs routedEventArgs)
    {
        Activate();
        UpdatePosition();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        _playbackModule.VideoWindow.SetSizeInPixels(MainGrid.Bounds.Width, MainGrid.Bounds.Height);
    }

    private void OnPositionChanged(object? sender, PixelPointEventArgs e)
    {
        UpdatePosition();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        _playbackModule.VideoWindow.Show();
        Activate();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _playbackModule.VideoWindow.Close();
    }

    private void UpdatePosition()
    {
        var pos = this.PointToScreen(MainGrid.Bounds.TopLeft);
        _playbackModule.VideoWindow.SetPositionInPixels(pos.X, pos.Y);
    }
}