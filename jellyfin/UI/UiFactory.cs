using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace JellyfinTizen.UI
{
    public static class UiFactory
    {
        private static readonly Color UnifiedBackdrop = new Color(9f / 255f, 15f / 255f, 31f / 255f, 1f);
        private static readonly Color UnifiedBackdropWash = new Color(22f / 255f, 35f / 255f, 68f / 255f, 0.08f);

        public static View CreateAtmosphericBackground()
        {
            var background = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                BackgroundColor = UnifiedBackdrop
            };

            background.Add(new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                BackgroundColor = UnifiedBackdropWash
            });
            return background;
        }

        public static View CreateCenteredPanel(int width = UiTheme.PanelWidth, int top = UiTheme.PanelTop)
        {
            int screenWidth = Window.Default.Size.Width;
            width = width > screenWidth - 80 ? screenWidth - 80 : width;

            return new View
            {
                WidthSpecification = width,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                PositionX = (screenWidth - width) / 2,
                PositionY = top,
                CornerRadius = UiTheme.PanelRadius,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BackgroundColor = UiTheme.Surface,
                BorderlineWidth = 1.5f,
                BorderlineColor = new Color(1f, 1f, 1f, 0.10f),
                Padding = new Extents(
                    UiTheme.SidePadding,
                    UiTheme.SidePadding,
                    UiTheme.PanelVerticalPadding,
                    UiTheme.PanelVerticalPadding
                ),
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    CellPadding = new Size2D(0, 24)
                }
            };
        }

        public static TextLabel CreateDisplayTitle(string text)
        {
            return new TextLabel(text)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                PointSize = UiTheme.DisplayTitle,
                TextColor = UiTheme.TextPrimary,
                HorizontalAlignment = HorizontalAlignment.Center,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word
            };
        }

        public static TextLabel CreateSubtitle(string text)
        {
            return new TextLabel(text)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                PointSize = UiTheme.Body,
                TextColor = UiTheme.TextSecondary,
                HorizontalAlignment = HorizontalAlignment.Center,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word
            };
        }

        public static void SetButtonFocusState(View button, bool focused)
        {
            MonochromeAuthFactory.SetButtonFocusState(button, focused);
        }

        public static View CreateTrackSelectionRow(
            string text,
            string id,
            float itemPointSize)
        {
            var row = new View
            {
                Name = id ?? string.Empty,
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = UiTheme.PlayerOverlayListRowHeight,
                BackgroundColor = Color.Black,
                CornerRadius =
                    UiTheme.PlayerOverlayListRowHeight / 2.0f,
                CornerRadiusPolicy =
                    VisualTransformPolicyType.Absolute,
                BorderlineWidth = 2.0f,
                BorderlineColor = Color.White,
                Margin = new Extents(
                    UiTheme.PlayerOverlayItemMarginHorizontal,
                    UiTheme.PlayerOverlayItemMarginHorizontal,
                    UiTheme.PlayerOverlayItemMarginVertical,
                    UiTheme.PlayerOverlayItemMarginVertical),
                Focusable = false,
                ClippingMode = ClippingModeType.ClipChildren
            };

            row.Add(new TextLabel(text ?? string.Empty)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                PointSize = itemPointSize,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Begin,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Extents(
                    UiTheme.PlayerOverlayItemPaddingLeft,
                    (ushort)0,
                    (ushort)0,
                    (ushort)0),
                Ellipsis = true
            });
            SetButtonFocusState(row, focused: false);
            return row;
        }
    }
}
