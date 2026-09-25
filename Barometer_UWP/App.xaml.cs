using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Barometer_UWP
{
    sealed partial class App : Application
    {
        public static App Current => (App)Application.Current;

        public ServiceContainer Services { get; } = new ServiceContainer();

        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    rootFrame.Navigate(typeof(Views.MainPage), e.Arguments);
                }
                Window.Current.Activate();
            }

            // Fire-and-forget async init of sensor + data store.
            _ = InitializeServicesAsync();
        }

        private async System.Threading.Tasks.Task InitializeServicesAsync()
        {
            try
            {
                await Helpers.Logger.InitializeAsync();
                await Services.DataService.InitializeAsync();
                await Services.SensorService.InitializeAsync();
            }
            catch (Exception ex)
            {
                try { await Helpers.Logger.LogErrorAsync("Init failed", ex); } catch { }
            }
        }

        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            _ = Services.DataService.SaveAsync();
            deferral.Complete();
        }
    }
}
