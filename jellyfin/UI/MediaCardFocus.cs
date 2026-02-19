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
                if (frameChild.Name != "CardInner")
                    continue;

                foreach (var innerChild in frameChild.Children)
                {
                    if (innerChild.Name == "CardContent")
                        return innerChild;
                }
            }

            return null;
        }

        public static void ApplyFrameFocus(View frame, Color fillColor, Color lineColor, bool lightweight)
        {
            if (frame == null)
                return;

            frame.CornerRadius = 16.0f;
            frame.BackgroundColor = fillColor;
            frame.BorderlineWidth = 2.0f;
            frame.BorderlineColor = lineColor;
            frame.BoxShadow = lightweight
                ? null
                : new Shadow(12.0f, new Color(lineColor.R, lineColor.G, lineColor.B, 0.36f), new Vector2(0, 0));
        }

        public static void ClearFrameFocus(View frame)
        {
            if (frame == null)
                return;

            frame.CornerRadius = 16.0f;
            frame.BackgroundColor = Color.Transparent;
            frame.BorderlineWidth = 0.0f;
            frame.BorderlineColor = Color.Transparent;
            frame.BoxShadow = null;
        }
    }
}
