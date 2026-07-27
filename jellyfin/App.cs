using System;
using Tizen.Applications;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Core;
using JellyfinTizen.Screens;
using JellyfinTizen.Utils;

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

        /// <summary>
        /// Tizen raises low-memory notifications (SoftWarning, then HardWarning) before
        /// the kernel OOM killer terminates the process. Shedding caches and decoded
        /// image textures here is the only chance to stay alive on a memory-capped TV.
        /// </summary>
        protected override void OnLowMemory(LowMemoryEventArgs e)
        {
            base.OnLowMemory(e);

            try
            {
                CacheHelper.Clear();
                int trimmed = NavigationService.ReleaseStackedScreenResources();

                bool aggressive = e?.LowMemoryStatus == LowMemoryStatus.HardWarning;

                Tizen.Log.Warn(
                    "Jellyfin",
                    $"Low memory ({e?.LowMemoryStatus}): cleared cache, trimmed {trimmed} stacked screen(s).");

                // A plain collect leaves the freed segments uncompacted, which does not
                // reduce the RSS the kernel accounts against the app's cgroup.
                System.Runtime.GCSettings.LargeObjectHeapCompactionMode =
                    System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(
                    GC.MaxGeneration,
                    aggressive ? GCCollectionMode.Forced : GCCollectionMode.Optimized,
                    blocking: true,
                    compacting: true);
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                Tizen.Log.Error("Jellyfin", $"Failed to handle low memory: {ex.Message}");
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

            // Initialize shared application services.
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
