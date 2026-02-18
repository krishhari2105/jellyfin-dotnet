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
            NavigationService.NotifyAppTerminating();
        }

        protected override void OnTerminate()
        {
            NavigationService.NotifyAppTerminating();
            base.OnTerminate();
        }

        void Initialize()
        {
            Window.Default.BackgroundColor = Color.Black;

            // Init global services
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
