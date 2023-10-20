using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using FlyleafLib.Controls.WPF;
using FlyleafLib.MediaPlayer;

namespace OsdDemo.Windows
{
    /// <summary>
    /// Interaction logic for VideoWindow.xaml
    /// </summary>
    public partial class VideoWindow : Window
    {
        public VideoWindow()
        {
            InitializeComponent();
        }

        public void SetSizeInPixels(double width, double height)
        {
            var size = TransformToPixels(width, height);
            Width = width;
            Height = height;
        }

        public void SetPositionInPixels(double x, double y)
        {
            var position = TransformToPixels(x, y);
            Left = position.X;
            Top = position.Y;
        }

        private (double X, double Y) TransformToPixels(
            double unitX,
            double unitY)
        {
            Matrix matrix;
            var source = PresentationSource.FromVisual(this);
            if (source != null)
            {
                matrix = source.CompositionTarget.TransformToDevice;
            }
            else
            {
                using (var src = new HwndSource(new HwndSourceParameters()))
                {
                    matrix = src.CompositionTarget.TransformToDevice;
                }
            }

            var pixelX = (unitX / matrix.M11);
            var pixelY = (unitY / matrix.M22);

            return (pixelX, pixelY);
        }
    }
}
