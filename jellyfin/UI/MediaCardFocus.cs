using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace JellyfinTizen.UI
{
    public static class MediaCardFocus
    {
        public static View GetCardFrame(View card)
        {
            if (card == null)
                return null;

            foreach (var child in card.Children)
            {
                if (child.Name == "CardFrame")
                    return child;
            }

            return null;
        }

        public static View GetCardContent(View card)
        {
            var frame = GetCardFrame(card);
            if (frame == null)
                return null;

            foreach (var frameChild in frame.Children)
            {
                if (frameChild.Name == "CardContent")
                    return frameChild;
            }

            return null;
        }

        public static void ApplyFrameFocus(View frame, Color lineColor)
        {
            if (frame == null)
                return;

            frame.CornerRadius = UiTheme.MediaCardRadius;
            frame.BackgroundColor = Color.Transparent;
            frame.BorderlineWidth = 2.0f;
            frame.BorderlineColor = lineColor;
            frame.BoxShadow = null;
        }

        public static void ClearFrameFocus(View frame)
        {
            if (frame == null)
                return;

            frame.CornerRadius = UiTheme.MediaCardRadius;
            frame.BackgroundColor = Color.Transparent;
            frame.BorderlineWidth = 0.0f;
            frame.BorderlineColor = Color.Transparent;
            frame.BoxShadow = null;
        }

        public static void ApplyCardFocus(View card, bool focused, float scale)
        {
            var frame = GetCardFrame(card);
            var scaleTarget = frame ?? card;
            scaleTarget.Scale = focused ? new Vector3(scale, scale, 1f) : Vector3.One;
            if (frame != null)
            {
                frame.PositionZ = focused ? 20 : 0;
                card.PositionZ = 0;
            }
            else
            {
                card.PositionZ = focused ? 20 : 0;
            }

            if (focused)
                ApplyFrameFocus(frame, UiTheme.MediaCardFocusBorder);
            else
                ClearFrameFocus(frame);
        }
    }
}
