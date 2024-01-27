using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace CommonControls.Wpf;
/// <summary>
/// Interaction logic for ReceiverControl.xaml
/// </summary>
public partial class ReceiverControl : UserControl
{
    public ReceiverControl()
    {
        InitializeComponent();
    }

    private void HyperlinkOnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
