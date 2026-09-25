using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Barometer_UWP.ViewModels;

namespace Barometer_UWP.Views
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsPage()
        {
            this.InitializeComponent();
            ViewModel = new SettingsViewModel();
            this.DataContext = ViewModel;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ViewModel.Initialize();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel?.Dispose();
            base.OnNavigatedFrom(e);
        }
    }
}