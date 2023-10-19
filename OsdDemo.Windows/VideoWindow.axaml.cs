using System.Reflection;
using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

using Gst;
using Application = Gst.Application;

namespace OsdDemo.Windows;

public partial class VideoWindow : Window
{
    public VideoWindow()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        var pipeline = new CustomWindowsGstPipeline();
        //var a = Functions.
        //var pipeline = new Pipeline();
        //pipeline.Add()
        //Element ret = Functions.ParseLaunch("playbin uri=playbin uri=http://ftp.halifax.rwth-aachen.de/blender/demo/movies/ToS/tears_of_steel_720p.mov");
        //ret.SetState(State.Playing);
        //Bus bus = ret.GetBus();
        //bus.WaitForEndOrError();
        //ret.SetState(State.Null);
    }
}