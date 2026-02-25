using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace JellyfinTizen.UI
{
    public static class UiFactory
    {
        private const int HiddenCursorWidth = 0;
        private const int VisibleCursorWidth = 2;
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

        public static TextField CreateInputField(string placeholder)
        {
            var field = new TextField
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = UiTheme.FieldHeight,
                PointSize = 30,
                BackgroundColor = UiTheme.SurfaceMuted,
                TextColor = UiTheme.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center,
                CornerRadius = 18.0f,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BorderlineWidth = 1.5f,
                BorderlineColor = new Color(1f, 1f, 1f, 0.12f),
                Padding = new Extents(24, 24, 26, 26),
                CursorWidth = HiddenCursorWidth,
                Focusable = true
            };

            var placeholderMap = new PropertyMap();
            placeholderMap.Add("text", new PropertyValue(placeholder));
            placeholderMap.Add("textColor", new PropertyValue(new Color(1f, 1f, 1f, 0.55f)));
            placeholderMap.Add("pointSize", new PropertyValue(30.0f));
            placeholderMap.Add("horizontalAlignment", new PropertyValue("BEGIN"));
            placeholderMap.Add("verticalAlignment", new PropertyValue("CENTER"));
            field.Placeholder = placeholderMap;
            field.DecorationBoundingBox = new Rectangle(
                30,
                26,
                4096,
                UiTheme.FieldHeight - 52
            );

            field.FocusGained += (_, _) =>
            {
                field.BackgroundColor = UiTheme.SurfaceFocused;
                field.BorderlineColor = UiTheme.AccentSoft;
                field.BorderlineWidth = 2.0f;
                SyncCursorVisibility(field);
            };

            field.FocusLost += (_, _) =>
            {
                field.BackgroundColor = UiTheme.SurfaceMuted;
                field.BorderlineColor = new Color(1f, 1f, 1f, 0.12f);
                field.BorderlineWidth = 1.5f;
            };
            field.TextChanged += (_, _) => SyncCursorVisibility(field);
            SyncCursorVisibility(field);

            return field;
        }

        public static View CreateInputFieldShell(string placeholder, out TextField field)
        {
            var shell = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = UiTheme.FieldHeight,
                BackgroundColor = UiTheme.SurfaceMuted,
                CornerRadius = 18.0f,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BorderlineWidth = 1.5f,
                BorderlineColor = new Color(1f, 1f, 1f, 0.12f),
                Padding = new Extents(30, 24, 0, 0)
            };

            field = new TextField
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 42,
                PositionY = (UiTheme.FieldHeight - 42) / 2,
                PointSize = 30,
                TextColor = UiTheme.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center,
                BackgroundColor = Color.Transparent,
                BorderlineWidth = 0.0f,
                CursorWidth = HiddenCursorWidth,
                Padding = new Extents(18, 0, 0, 0),
                Focusable = true
            };
            var input = field;

            var placeholderMap = new PropertyMap();
            placeholderMap.Add("text", new PropertyValue(placeholder));
            placeholderMap.Add("textColor", new PropertyValue(new Color(1f, 1f, 1f, 0.55f)));
            placeholderMap.Add("pointSize", new PropertyValue(30.0f));
            placeholderMap.Add("horizontalAlignment", new PropertyValue("BEGIN"));
            placeholderMap.Add("verticalAlignment", new PropertyValue("CENTER"));
            input.Placeholder = placeholderMap;
            input.DecorationBoundingBox = new Rectangle(20, 0, 4076, 42);

            input.FocusGained += (_, _) =>
            {
                shell.BackgroundColor = UiTheme.SurfaceFocused;
                shell.BorderlineColor = UiTheme.AccentSoft;
                shell.BorderlineWidth = 2.0f;
                SyncCursorVisibility(input);
            };

            input.FocusLost += (_, _) =>
            {
                shell.BackgroundColor = UiTheme.SurfaceMuted;
                shell.BorderlineColor = new Color(1f, 1f, 1f, 0.12f);
                shell.BorderlineWidth = 1.5f;
            };
            input.TextChanged += (_, _) => SyncCursorVisibility(input);
            SyncCursorVisibility(input);

            shell.Add(input);
            return shell;
        }

        private static void SyncCursorVisibility(TextField field)
        {
            if (field == null)
                return;

            field.CursorWidth = string.IsNullOrEmpty(field.Text)
                ? HiddenCursorWidth
                : VisibleCursorWidth;
        }

        public static View CreateButton(string text, out TextLabel label, bool primary)
        {
            var button = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = UiTheme.ButtonHeight,
                CornerRadius = UiTheme.ButtonHeight / 2.0f,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BorderlineWidth = 2.0f,
                Focusable = true
            };

            label = new TextLabel(text)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                PointSize = 31,
                TextColor = UiTheme.TextPrimary
            };

            SetButtonFocusState(button, primary, false);
            button.Add(label);
            return button;
        }

        public static void SetButtonFocusState(View button, bool primary, bool focused)
        {
            MonochromeAuthFactory.SetButtonFocusState(button, primary, focused);
        }

        public static TextLabel CreateErrorLabel()
        {
            return new TextLabel(string.Empty)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                PointSize = UiTheme.Caption,
                TextColor = UiTheme.Danger,
                HorizontalAlignment = HorizontalAlignment.Center,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word
            };
        }
    }
}
