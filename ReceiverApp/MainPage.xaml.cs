
using ReceiverApp.Platforms.Android.ViewModels;

namespace ReceiverApp
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
#if ANDROID
            var context = Android.App.Application.Context;
            var viewModel = new MainViewModel(context);
            BindingContext = viewModel;
#endif
        }
    }

}
