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
        private const int AuthPanelWidth = 760;
        private const int AuthPanelYOffset = 0;
        private const int AuthPanelSidePadding = 52;
        private const int AuthPanelVerticalPadding = 42;
        private const int AuthPanelGap = 18;
        private const int AuthFieldHeight = 82;
        private const int AuthButtonHeight = 74;
        private const float AuthTitleSize = 42.0f;
        private const float AuthSubtitleSize = 22.0f;
        private static readonly bool EnableAuthBackgroundBlur = false;
        private static readonly bool EnableAuthBlurDiagnostics = false;
        private static readonly Color AuthBackdropColor = new Color(9f / 255f, 15f / 255f, 31f / 255f, 1f);
        private static readonly Color AuthBackdropBlueWash = new Color(0f, 0f, 0f, 0f);
        private static readonly Color AuthPanelColor = new Color(7f / 255f, 13f / 255f, 28f / 255f, 0.62f);
        private static readonly Color AuthPanelFallbackColor = new Color(7f / 255f, 13f / 255f, 28f / 255f, 0.62f);
        private static readonly Color AuthPanelFallbackBorder = new Color(1f, 1f, 1f, 0.24f);
        private static readonly Color AuthFieldColor = new Color(11f / 255f, 18f / 255f, 34f / 255f, 1f);
        private static readonly Color AuthFieldFocusColor = new Color(18f / 255f, 28f / 255f, 49f / 255f, 1f);
        private static bool _runtimeBlurActive;
        public const float PanelCornerRadius = 24.0f;
        public const float PanelBorderWidth = 1.4f;
        public static readonly Color PanelFallbackColor = new Color(7f / 255f, 13f / 255f, 28f / 255f, 0.62f);
        public static readonly Color PanelFallbackBorder = new Color(1f, 1f, 1f, 0.24f);

        public static View CreateBackground()
        {
            _runtimeBlurActive = EnableAuthBackgroundBlur;

            var background = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                BackgroundColor = AuthBackdropColor
            };

            background.Add(new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                BackgroundColor = AuthBackdropBlueWash
            });

            if (EnableAuthBlurDiagnostics)
                background.Add(CreateBlurDiagnosticBars());

            if (EnableAuthBackgroundBlur)
                _runtimeBlurActive = TryAttachBackgroundBlur(background);
            return background;
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
                BackgroundColor = _runtimeBlurActive ? AuthPanelColor : AuthPanelFallbackColor,
                BorderlineWidth = PanelBorderWidth,
                BorderlineColor = _runtimeBlurActive
                    ? new Color(1f, 1f, 1f, 0.18f)
                    : AuthPanelFallbackBorder,
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
                HeightSpecification = 38,
                PositionY = (AuthFieldHeight - 38) / 2,
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

            var placeholderMap = new PropertyMap();
            placeholderMap.Add("text", new PropertyValue(placeholder));
            placeholderMap.Add("textColor", new PropertyValue(new Color(1f, 1f, 1f, 0.48f)));
            placeholderMap.Add("pointSize", new PropertyValue(26.0f));
            placeholderMap.Add("horizontalAlignment", new PropertyValue("BEGIN"));
            placeholderMap.Add("verticalAlignment", new PropertyValue("CENTER"));
            input.Placeholder = placeholderMap;
            input.DecorationBoundingBox = new Rectangle(12, 0, 4076, 38);

            input.FocusGained += (_, _) =>
            {
                shell.BackgroundColor = AuthFieldFocusColor;
                shell.BorderlineColor = new Color(1f, 1f, 1f, 0.64f);
                shell.BorderlineWidth = 2.0f;
                SyncCursorVisibility(input);
            };

            input.FocusLost += (_, _) =>
            {
                shell.BackgroundColor = AuthFieldColor;
                shell.BorderlineColor = new Color(1f, 1f, 1f, 0.16f);
                shell.BorderlineWidth = 1.4f;
            };
            input.TextChanged += (_, _) => SyncCursorVisibility(input);
            SyncCursorVisibility(input);

            shell.Add(input);
            return shell;
        }

        public static View CreateButton(string text, out TextLabel label, bool primary)
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

            SetButtonFocusState(button, primary, focused: false);
            button.Add(label);
            return button;
        }

        public static void SetButtonFocusState(View button, bool primary, bool focused)
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

        private static void SyncCursorVisibility(TextField field)
        {
            if (field == null)
                return;

            field.CursorWidth = string.IsNullOrEmpty(field.Text)
                ? HiddenCursorWidth
                : VisibleCursorWidth;
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

        private static bool TryAttachBackgroundBlur(View background)
        {
            if (background == null)
                return false;

            try
            {
                Type blurType = ResolveGaussianBlurViewType();
                if (blurType == null)
                    return false;

                object blurObject = Activator.CreateInstance(blurType);
                if (blurObject is not View blurView)
                    return false;

                blurView.WidthResizePolicy = ResizePolicyType.FillToParent;
                blurView.HeightResizePolicy = ResizePolicyType.FillToParent;
                blurView.BackgroundColor = AuthBackdropColor;
                blurView.Opacity = 0.36f;

                SetNumericPropertyIfPresent(blurObject, "BlurStrength", 0.16f);
                SetNumericPropertyIfPresent(blurObject, "BlurRadius", 14.0f);
                SetNumericPropertyIfPresent(blurObject, "BlurDownscaleFactor", 1.0f);

                background.Add(blurView);
                bool activated = InvokeParameterlessMethodIfPresent(blurObject, "ActivateOnce");
                if (!activated)
                {
                    activated = InvokeParameterlessMethodIfPresent(blurObject, "Activate");
                }

                if (!activated)
                {
                    background.Remove(blurView);
                    blurView.Dispose();
                    return false;
                }

                return true;
            }
            catch
            {
                // Blur is optional and device-dependent; ignore unsupported paths.
                return false;
            }
        }

        private static View CreateBlurDiagnosticBars()
        {
            int screenWidth = Window.Default.Size.Width;
            int screenHeight = Window.Default.Size.Height;
            const int barCount = 14;
            const int barWidth = 20;
            const int barHeight = 30;
            const int barGap = 10;
            int rowWidth = (barCount * barWidth) + ((barCount - 1) * barGap);

            var row = new View
            {
                WidthSpecification = rowWidth,
                HeightSpecification = barHeight,
                PositionX = (screenWidth - rowWidth) / 2,
                PositionY = (screenHeight / 2) + 118
            };

            for (int i = 0; i < barCount; i++)
            {
                var bar = new View
                {
                    WidthSpecification = barWidth,
                    HeightSpecification = barHeight,
                    PositionX = i * (barWidth + barGap),
                    CornerRadius = 6.0f,
                    CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                    BackgroundColor = (i % 2 == 0)
                        ? new Color(1f, 1f, 1f, 0.42f)
                        : new Color(70f / 255f, 130f / 255f, 1f, 0.42f)
                };
                row.Add(bar);
            }

            return row;
        }

        private static Type ResolveGaussianBlurViewType()
        {
            Type type = Type.GetType("Tizen.NUI.GaussianBlurView, Tizen.NUI", false)
                ?? Type.GetType("Tizen.NUI.BaseComponents.GaussianBlurView, Tizen.NUI", false);
            if (type != null)
                return type;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                var assembly = assemblies[i];
                var assemblyName = assembly.GetName().Name;
                if (string.IsNullOrEmpty(assemblyName) || !assemblyName.StartsWith("Tizen.NUI", StringComparison.Ordinal))
                    continue;

                type = assembly.GetType("Tizen.NUI.GaussianBlurView", false)
                    ?? assembly.GetType("Tizen.NUI.BaseComponents.GaussianBlurView", false);
                if (type != null)
                    return type;

                try
                {
                    var types = assembly.GetTypes();
                    for (int j = 0; j < types.Length; j++)
                    {
                        var candidate = types[j];
                        if (candidate != null && candidate.Name == "GaussianBlurView")
                            return candidate;
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    var partialTypes = ex.Types;
                    if (partialTypes == null)
                        continue;

                    for (int j = 0; j < partialTypes.Length; j++)
                    {
                        var candidate = partialTypes[j];
                        if (candidate != null && candidate.Name == "GaussianBlurView")
                            return candidate;
                    }
                }
            }

            return null;
        }

        private static bool SetNumericPropertyIfPresent(object target, string propertyName, float value)
        {
            if (target == null || string.IsNullOrEmpty(propertyName))
                return false;

            var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null || !property.CanWrite)
                return false;

            object convertedValue;
            var type = property.PropertyType;
            if (type == typeof(float))
                convertedValue = value;
            else if (type == typeof(double))
                convertedValue = (double)value;
            else if (type == typeof(int))
                convertedValue = (int)Math.Round(value);
            else if (type == typeof(uint))
                convertedValue = (uint)Math.Max(0, (int)Math.Round(value));
            else
                return false;

            try
            {
                property.SetValue(target, convertedValue);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool InvokeParameterlessMethodIfPresent(object target, string methodName)
        {
            if (target == null || string.IsNullOrEmpty(methodName))
                return false;

            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null
            );
            if (method == null)
                return false;

            try
            {
                method.Invoke(target, null);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
