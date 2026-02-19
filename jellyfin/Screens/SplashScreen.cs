using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.UI;

namespace JellyfinTizen.Screens
{
    public class SplashScreen : ScreenBase
    {
        public SplashScreen()
        {
            var root = UiFactory.CreateAtmosphericBackground();
            var label = new TextLabel("Jellyfin")
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,

                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,

                PointSize = 64,
                TextColor = UiTheme.TextPrimary
            };

            root.Add(label);
            Add(root);
        }
    }
}
