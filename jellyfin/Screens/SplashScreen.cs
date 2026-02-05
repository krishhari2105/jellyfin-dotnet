using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace JellyfinTizen.Screens
{
    public class SplashScreen : ScreenBase
    {
        public SplashScreen()
        {
            var label = new TextLabel("Jellyfin Native (Tizen)")
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,

                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,

                PointSize = 48,
                TextColor = Color.White
            };

            Add(label);
        }
    }
}
