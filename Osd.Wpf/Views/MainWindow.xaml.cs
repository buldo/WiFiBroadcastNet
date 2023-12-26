using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Osd.Wpf.ViewModels;

namespace Osd.Wpf;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        //MediaPlayer player = new MediaPlayer();
        //player.Open(new Uri(@"C:\testData\media\Eurotrip.mkv", UriKind.Relative));
        //VideoDrawing drawing = new VideoDrawing();
        //drawing.Rect = new Rect(0, 0, 300, 200);
        //drawing.Player = player;
        //player.Play();
        //DrawingBrush brush = new DrawingBrush(drawing);
        //this.Background = brush;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        App.Current.Shutdown();
    }
}