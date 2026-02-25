using Tizen.NUI;
using JellyfinTizen.Core;
using JellyfinTizen.UI;

namespace JellyfinTizen.Screens
{
    public class LoadingScreen : ScreenBase, IKeyHandler
    {
        private readonly AppleTvLoadingVisual _loadingVisual;

        public LoadingScreen(string message)
        {
            _loadingVisual = new AppleTvLoadingVisual(message);
            Add(_loadingVisual.Root);
        }

        public override void OnShow()
        {
            _loadingVisual.Start();
        }

        public override void OnHide()
        {
            _loadingVisual.Stop();
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
