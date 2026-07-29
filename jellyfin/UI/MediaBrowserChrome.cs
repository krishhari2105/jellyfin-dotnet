using System;
using System.Collections.Generic;
using System.IO;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Core;

namespace JellyfinTizen.UI
{
    public static class MediaBrowserChrome
    {
        public static View CreateTopBar(
            string titleText,
            int height,
            int leftPadding,
            int rightPadding,
            int positionZ,
            bool centerTitle,
            bool includeLeftSpacer,
            int leftBlendOffsetX,
            int leftBlendOffsetY,
            out View settingsButton)
        {
            var topBar = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = height,
                PositionZ = positionZ,
                BackgroundColor = Color.Transparent,
                ClippingMode = ClippingModeType.ClipChildren
            };

            var contentRow = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                BackgroundColor = Color.Transparent,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    CellPadding = new Size2D(16, 0)
                },
                Padding = new Extents((ushort)leftPadding, (ushort)rightPadding, 16, 0)
            };

            if (includeLeftSpacer)
            {
                contentRow.Add(new View
                {
                    WidthSpecification = 50,
                    HeightSpecification = 50
                });
            }

            var title = new TextLabel(titleText ?? string.Empty)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                PointSize = UiTheme.MediaTopBarTitle,
                TextColor = UiTheme.TextPrimary,
                HorizontalAlignment = centerTitle ? HorizontalAlignment.Center : HorizontalAlignment.Begin,
                VerticalAlignment = VerticalAlignment.Center,
                Ellipsis = false
            };
            title.SetFontStyle(new Tizen.NUI.Text.FontStyle { Weight = FontWeightType.Bold });
            contentRow.Add(title);

            settingsButton = CreateProfileButton();
            contentRow.Add(settingsButton);

            topBar.Add(contentRow);
            return topBar;
        }

        public static View CreateProfileButton(int size = 50)
        {
            var sharedResPath = Tizen.Applications.Application.Current.DirectoryInfo.SharedResource;
            var button = new View
            {
                WidthSpecification = size,
                HeightSpecification = size,
                BackgroundColor = Color.Transparent,
                Focusable = true
            };

            // Requested at roughly the rendered size rather than 512px: the button is only
            // `size` pixels wide, and a large source costs the server a resize, the network
            // a bigger payload, and the alpha mask more work on load.
            var avatarUrl = AppState.GetUserAvatarUrl(Math.Max(96, size * 2));
            var hasAvatar = !string.IsNullOrWhiteSpace(avatarUrl);
            var settingsIconPath = sharedResPath + "settings.svg";
            var fallbackIconPath = sharedResPath + "jellyfin.png";
            var iconUrl = hasAvatar
                ? avatarUrl
                : (File.Exists(settingsIconPath) ? settingsIconPath : fallbackIconPath);
            var icon = new ImageView
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                ResourceUrl = iconUrl,
                FittingMode = FittingModeType.ShrinkToFit,
                SamplingMode = SamplingModeType.BoxThenLanczos,
                // Without this the avatar is released whenever the screen detaches from the
                // scene, so it visibly re-loaded (and re-masked) on every back-navigation
                // while the surrounding posters were already warm.
                ReleasePolicy = ReleasePolicyType.Destroyed,
                Padding = hasAvatar ? new Extents(0, 0, 0, 0) : new Extents(10, 10, 10, 10),
                AlphaMaskURL = sharedResPath + "avatar-mask.png",
                CropToMask = true,
                MaskingMode = ImageView.MaskingModeType.MaskingOnLoading
            };

            button.Add(icon);
            return button;
        }

        public static void SetProfileButtonFocusState(
            View button,
            bool focused)
        {
            if (button == null)
                return;

            button.Scale = Vector3.One;
            button.CornerRadius = button.HeightSpecification > 0
                ? button.HeightSpecification / 2.0f
                : 25.0f;
            button.CornerRadiusPolicy =
                VisualTransformPolicyType.Absolute;
            button.BackgroundColor = focused
                ? UiTheme.MediaCardFocusFill
                : Color.Transparent;
            button.BorderlineColor = UiTheme.MediaCardFocusBorder;
            button.BorderlineWidth = focused ? 2.0f : 0.0f;
        }

        public static View CreateSettingsPanel(int positionX, int positionY, int positionZ = 0)
        {
            return new View
            {
                WidthSpecification = 420,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                BackgroundColor = new Color(0, 0, 0, 1.0f),
                PositionX = positionX,
                PositionY = positionY,
                PositionZ = positionZ,
                CornerRadius = 14f,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                ClippingMode = ClippingModeType.ClipChildren,
                BorderlineWidth = 1.5f,
                BorderlineColor = new Color(0.2f, 0.2f, 0.2f, 1f),
                Padding = new Extents(16, 16, 16, 16),
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    CellPadding = new Size2D(0, 12)
                },
                Opacity = 1.0f
            };
        }

        public static TextLabel CreateSettingsPanelTitle()
        {
            return new TextLabel("Settings")
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 48,
                PointSize = UiTheme.SettingsPanelTitle,
                TextColor = UiTheme.TextPrimary,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        public static TextLabel CreateRowTitle(string text, int sidePadding)
        {
            return new TextLabel(text ?? string.Empty)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = UiTheme.MediaRowTitleHeight,
                PointSize = UiTheme.MediaRowTitle,
                TextColor = UiTheme.TextPrimary,
                HorizontalAlignment = HorizontalAlignment.Begin,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Extents((ushort)sidePadding, (ushort)sidePadding, 0, 0)
            };
        }

        public static View CreateSettingsOptionsList()
        {
            return new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    CellPadding = new Size2D(0, 10)
                }
            };
        }

        public static View CreateSettingsOption(string text)
        {
            var row = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 56,
                BackgroundColor = MonochromeAuthFactory.PanelFallbackColor,
                BorderlineWidth = 2.0f,
                BorderlineColor = MonochromeAuthFactory.PanelFallbackBorder,
                CornerRadius = 28.0f,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                ClippingMode = ClippingModeType.ClipChildren
            };

            var label = new TextLabel(text)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                PointSize = UiTheme.SettingsPanelOption,
                TextColor = new Color(1, 1, 1, 0.9f),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            row.Add(label);
            UiFactory.SetButtonFocusState(row, focused: false);
            return row;
        }

        public static void UpdateSettingsHighlight(List<View> options, int selectedIndex)
        {
            if (options == null)
                return;

            for (int i = 0; i < options.Count; i++)
            {
                bool focused = i == selectedIndex;
                UiFactory.SetButtonFocusState(options[i], focused: focused);

                if (options[i].ChildCount > 0 && options[i].GetChildAt(0) is TextLabel label)
                {
                    label.TextColor = focused ? Color.Black : Color.White;
                }
            }
        }
    }
}
