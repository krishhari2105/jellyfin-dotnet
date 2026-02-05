using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace JellyfinTizen.Screens
{
    public abstract class ScreenBase : View
    {
        protected ScreenBase()
        {
            WidthResizePolicy = ResizePolicyType.FillToParent;
            HeightResizePolicy = ResizePolicyType.FillToParent;
            BackgroundColor = Color.Black;
            Focusable = true;
        }

        public virtual void OnShow() { }
        public virtual void OnHide() { }
    }
}
