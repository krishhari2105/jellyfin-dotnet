using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Core;
using JellyfinTizen.Screens;

namespace JellyfinTizen
{
    class App : NUIApplication
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            Initialize();
        }

        protected override void OnPause()
        {
            base.OnPause();
            AppLifecycle.TransitionToSuspended();
            NavigationService.NotifyAppPaused();
        }

        protected override void OnResume()
        {
            base.OnResume();
            try
            {
                AppState.OnAppResumed();
            }
            catch (System.Exception ex)
            {
                Tizen.Log.Error("Jellyfin", $"Failed to handle App resume: {ex.Message}");
            }
        }

        protected override void OnTerminate()
        {
            NavigationService.NotifyAppTerminating();
            AppState.Shutdown();
            base.OnTerminate();
        }

        void Initialize()
        {
            Window.Default.BackgroundColor = Color.Black;

            // Init global services, including Tailscale launcher and local proxy
            AppState.Init();
            NavigationService.Init(Window.Default);

            // Start screen
            NavigationService.Navigate(new StartupScreen());
        }

        static void Main(string[] args)
        {
            new App().Run(args);
        }
    }
}
