using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Barometer_UWP.ViewModels;

namespace Barometer_UWP.Views
{
    public sealed partial class MainPage : Page
    {
        public MainViewModel ViewModel { get; }

        public MainPage()
        {
            this.InitializeComponent();
            ViewModel = new MainViewModel();
            this.DataContext = ViewModel;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel?.Dispose();
            base.OnNavigatedFrom(e);
        }

        private void GraphicsButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(GraphicsPage));
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPage));
        }

        private void ShareButton_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder for share functionality
            // This would use Windows.ApplicationModel.DataTransfer
        }
    }
}