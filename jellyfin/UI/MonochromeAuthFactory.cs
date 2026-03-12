using System;
using System.Reflection;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace JellyfinTizen.UI
{
    public static class MonochromeAuthFactory
    {
        private const int HiddenCursorWidth = 0;
        private const int VisibleCursorWidth = 2;
        private const int AuthPlaceholderCursorHeight = 30;
        private const int AuthPlaceholderCursorBlinkIntervalMs = 530;
        private const int AuthPanelWidth = 760;
        private const int AuthPanelYOffset = 0;
        private const int AuthPanelSidePadding = 52;
        private const int AuthPanelVerticalPadding = 42;
        private const int AuthPanelGap = 18;
        private const int AuthFieldHeight = 82;
        private const int AuthButtonHeight = 74;
        private const float AuthTitleSize = 42.0f;
        private const float AuthSubtitleSize = 22.0f;
        private static readonly Color AuthBackdropColor = new Color(9f / 255f, 15f / 255f, 31f / 255f, 1f);
        private static readonly Color AuthFieldColor = new Color(11f / 255f, 18f / 255f, 34f / 255f, 1f);
        private static readonly Color AuthFieldFocusColor = new Color(18f / 255f, 28f / 255f, 49f / 255f, 1f);
        private static readonly Color AuthCursorFallbackColor = new Color(1f, 1f, 1f, 0.96f);
        public const float PanelCornerRadius = 24.0f;
        public const float PanelBorderWidth = 1.4f;
        public static readonly Color PanelFallbackColor = new Color(7f / 255f, 13f / 255f, 28f / 255f, 0.62f);
        public static readonly Color PanelFallbackBorder = new Color(1f, 1f, 1f, 0.24f);

        public static View CreateBackground()
        {
            return new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                BackgroundColor = AuthBackdropColor
            };
        }

        public static View CreatePanel(int width = AuthPanelWidth, int yOffset = AuthPanelYOffset)
        {
            int screenWidth = Window.Default.Size.Width;
            width = width > screenWidth - 80 ? screenWidth - 80 : width;

            return new View
            {
                WidthSpecification = width,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                PositionX = 0,
                PositionY = yOffset,
                ParentOrigin = ParentOrigin.Center,
                PivotPoint = PivotPoint.Center,
                PositionUsesPivotPoint = true,
                CornerRadius = PanelCornerRadius,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BackgroundColor = PanelFallbackColor,
                BorderlineWidth = PanelBorderWidth,
                BorderlineColor = PanelFallbackBorder,
                Padding = new Extents(
                    AuthPanelSidePadding,
                    AuthPanelSidePadding,
                    AuthPanelVerticalPadding,
                    AuthPanelVerticalPadding
                ),
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    CellPadding = new Size2D(0, AuthPanelGap)
                }
            };
        }

        public static TextLabel CreateTitle(string text)
        {
            return new TextLabel(text)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                PointSize = AuthTitleSize,
                TextColor = new Color(1f, 1f, 1f, 0.96f),
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
                PointSize = AuthSubtitleSize,
                TextColor = new Color(1f, 1f, 1f, 0.70f),
                HorizontalAlignment = HorizontalAlignment.Center,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word
            };
        }

        public static View CreateInputFieldShell(string placeholder, out TextField field)
        {
            int textFieldHeight = 38;
            var shell = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = AuthFieldHeight,
                BackgroundColor = AuthFieldColor,
                CornerRadius = 20.0f,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BorderlineWidth = 1.4f,
                BorderlineColor = new Color(1f, 1f, 1f, 0.16f),
                Padding = new Extents(24, 20, 0, 0)
            };

            field = new TextField
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = textFieldHeight,
                PositionY = (AuthFieldHeight - textFieldHeight) / 2,
                PointSize = 26,
                TextColor = new Color(1f, 1f, 1f, 0.96f),
                VerticalAlignment = VerticalAlignment.Center,
                BackgroundColor = Color.Transparent,
                BorderlineWidth = 0.0f,
                CursorWidth = HiddenCursorWidth,
                Padding = new Extents(12, 0, 0, 0),
                Focusable = true
            };
            var input = field;

            var placeholderMap = CreateInputPlaceholderMap(placeholder);
            var hiddenPlaceholderMap = CreateInputPlaceholderMap(string.Empty);
            input.Placeholder = placeholderMap;
            input.DecorationBoundingBox = new Rectangle(12, 0, 4076, textFieldHeight);
            var placeholderCursorColor = TryResolveNativeCursorColor(input, out var nativeCursorColor)
                ? nativeCursorColor
                : AuthCursorFallbackColor;

            var placeholderCursor = new View
            {
                WidthSpecification = VisibleCursorWidth,
                HeightSpecification = AuthPlaceholderCursorHeight,
                PositionX = 12,
                PositionY = input.PositionY + ((textFieldHeight - AuthPlaceholderCursorHeight) / 2),
                BackgroundColor = placeholderCursorColor,
                CornerRadius = 1.0f,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                Opacity = 0.0f
            };
            bool placeholderCursorVisible = true;
            var placeholderCursorBlinkTimer = new Timer(AuthPlaceholderCursorBlinkIntervalMs);
            placeholderCursorBlinkTimer.Tick += (_, _) =>
            {
                placeholderCursorVisible = !placeholderCursorVisible;
                placeholderCursor.Opacity = placeholderCursorVisible ? 1.0f : 0.0f;
                return true;
            };

            bool isFocused = false;
            void SyncInputVisualState()
            {
                bool isEmpty = string.IsNullOrEmpty(input.Text);
                bool showPlaceholderCursor = isFocused && isEmpty;
                input.CursorWidth = isFocused && !isEmpty
                    ? VisibleCursorWidth
                    : HiddenCursorWidth;
                input.Placeholder = !isFocused && isEmpty
                    ? placeholderMap
                    : hiddenPlaceholderMap;
                if (showPlaceholderCursor)
                {
                    placeholderCursorVisible = true;
                    placeholderCursor.Opacity = 1.0f;
                    placeholderCursorBlinkTimer.Stop();
                    placeholderCursorBlinkTimer.Start();
                }
                else
                {
                    placeholderCursorBlinkTimer.Stop();
                    placeholderCursor.Opacity = 0.0f;
                }
            }

            input.FocusGained += (_, _) =>
            {
                isFocused = true;
                shell.BackgroundColor = AuthFieldFocusColor;
                shell.BorderlineColor = new Color(1f, 1f, 1f, 0.64f);
                shell.BorderlineWidth = 2.0f;
                SyncInputVisualState();
            };

            input.FocusLost += (_, _) =>
            {
                isFocused = false;
                shell.BackgroundColor = AuthFieldColor;
                shell.BorderlineColor = new Color(1f, 1f, 1f, 0.16f);
                shell.BorderlineWidth = 1.4f;
                SyncInputVisualState();
            };
            input.TextChanged += (_, _) => SyncInputVisualState();
            SyncInputVisualState();

            shell.Add(input);
            shell.Add(placeholderCursor);
            return shell;
        }

        public static View CreateButton(string text, out TextLabel label)
        {
            int buttonWidth = EstimatePillWidth(text);
            var button = new View
            {
                WidthSpecification = buttonWidth,
                HeightSpecification = AuthButtonHeight,
                CornerRadius = AuthButtonHeight / 2.0f,
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
                PointSize = 26.0f,
                TextColor = new Color(1f, 1f, 1f, 0.96f)
            };

            SetButtonFocusState(button, focused: false);
            button.Add(label);
            return button;
        }

        public static void SetButtonFocusState(View button, bool focused)
        {
            if (button == null)
                return;

            float pillRadius = ResolvePillRadius(button);
            button.CornerRadius = pillRadius;
            button.CornerRadiusPolicy = VisualTransformPolicyType.Absolute;

            if (focused)
            {
                // Focused: solid white pill with dark text.
                button.BackgroundColor = new Color(1f, 1f, 1f, 1f);
                button.BorderlineColor = new Color(1f, 1f, 1f, 1f);
                button.BorderlineWidth = 0.0f;
            }
            else
            {
                // Unfocused: match startup panel fake-blur fallback styling.
                button.BackgroundColor = PanelFallbackColor;
                button.BorderlineColor = PanelFallbackBorder;
                button.BorderlineWidth = 2.0f;
            }

            var textColor = focused
                ? new Color(0f, 0f, 0f, 1f)
                : new Color(1f, 1f, 1f, 1f);
            ApplyTextColor(button, textColor);
        }

        public static TextLabel CreateErrorLabel()
        {
            return new TextLabel(string.Empty)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                PointSize = 20.0f,
                TextColor = new Color(1f, 1f, 1f, 0.86f),
                HorizontalAlignment = HorizontalAlignment.Center,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word
            };
        }

        private static PropertyMap CreateInputPlaceholderMap(string text)
        {
            var placeholderMap = new PropertyMap();
            placeholderMap.Add("text", new PropertyValue(text ?? string.Empty));
            placeholderMap.Add("textColor", new PropertyValue(new Color(1f, 1f, 1f, 0.48f)));
            placeholderMap.Add("pointSize", new PropertyValue(26.0f));
            placeholderMap.Add("horizontalAlignment", new PropertyValue("BEGIN"));
            placeholderMap.Add("verticalAlignment", new PropertyValue("CENTER"));
            return placeholderMap;
        }

        private static float ResolvePillRadius(View button)
        {
            int h = (int)button.HeightSpecification;
            if (h <= 0)
                h = AuthButtonHeight;
            return Math.Max(18.0f, h / 2.0f);
        }

        private static int EstimatePillWidth(string text)
        {
            int length = string.IsNullOrWhiteSpace(text) ? 8 : text.Trim().Length;
            int estimated = 130 + (length * 24);
            return Math.Clamp(estimated, 220, 620);
        }

        private static void ApplyTextColor(View view, Color color)
        {
            if (view == null)
                return;

            if (view is TextLabel label)
                label.TextColor = color;

            uint childCount = view.ChildCount;
            for (uint i = 0; i < childCount; i++)
            {
                if (view.GetChildAt(i) is View child)
                    ApplyTextColor(child, color);
            }
        }

        private static bool TryResolveNativeCursorColor(TextField field, out Color color)
        {
            if (TryReadColorProperty(field, "PrimaryCursorColor", out color))
                return true;
            if (TryReadColorProperty(field, "SecondaryCursorColor", out color))
                return true;
            return TryReadColorProperty(field, "CursorColor", out color);
        }

        private static bool TryReadColorProperty(object target, string propertyName, out Color color)
        {
            color = default;

            if (target == null || string.IsNullOrEmpty(propertyName))
                return false;

            var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null || !property.CanRead)
                return false;

            try
            {
                var value = property.GetValue(target);
                if (value is Color resolvedColor)
                {
                    color = resolvedColor;
                    return true;
                }

                if (value is Vector4 vector)
                {
                    color = new Color(vector.X, vector.Y, vector.Z, vector.W);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }
    }
}
