using Avalonia.Controls;
using OsdDemo.ViewModels;

namespace OsdDemo;
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _dataContext;
    public MainWindow()
    {
        InitializeComponent();
        _dataContext = new MainWindowViewModel();
        DataContext = _dataContext;
    }
}