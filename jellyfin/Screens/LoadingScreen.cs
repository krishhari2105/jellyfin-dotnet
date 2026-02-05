using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Core;

namespace JellyfinTizen.Screens
{
    public class LoadingScreen : ScreenBase, IKeyHandler
    {
        public LoadingScreen(string message)
        {
            var label = new TextLabel(message)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                PointSize = 44,
                TextColor = Color.White
            };

            Add(label);
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
