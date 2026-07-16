using JellyfinTizen.Core;

namespace JellyfinTizen.Screens
{
    public class LoadingScreen : ScreenBase, IKeyHandler
    {
        private readonly string _message;

        public LoadingScreen(string message)
        {
            _message = message;
        }

        public override void OnShow()
        {
            // Use the shared persistent spinner overlay — same singleton used by detail
            // screens' OnShow, so the animation continues seamlessly when we navigate
            // from this LoadingScreen to a target screen that also calls ShowLoadingOverlay.
            NavigationService.ShowLoadingOverlay(_message);
        }

        public override void OnHide()
        {
            NavigationService.HideLoadingOverlay();
        }

        public void HandleKey(AppKey key)
        {
            if (key == AppKey.Back)
            {
                NavigationService.NavigateBack();
            }
        }
    }
}
