using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Core;
using JellyfinTizen.Models;
using JellyfinTizen.UI;
using JellyfinTizen.Utils;
using IOPath = System.IO.Path;

namespace JellyfinTizen.Screens
{
    // Self-contained Netflix-style preview overlay hosted on top of VideoPlayerScreen's
    // video plane when the player runs in preview mode. Deliberately NOT built from
    // DetailsScreenBase.BuildDetailsOverlay (that path is coupled to the full details-screen
    // flow and its Play/Resume/Audio/Subtitle/Version buttons navigate to a new player).
    // Shows backdrop + title/logo + synopsis + Play/Resume only; audio/subtitle/version
    // selection is deferred to the in-player OSD per the approved plan.
    //
    // Key events are routed to this view by VideoPlayerScreen.HandleKey (the screen is the
    // registered IKeyHandler); this component only manages its own button focus visuals.
    public class PreviewOverlay : View
    {
        private const int ActionButtonHeight = 70;
        private const int ButtonIconSize = 40;
        private const int ButtonIconLabelGap = 8;
        private const float ButtonFocusScale = 1.08f;

        private readonly List<View> _buttons = new();
        // Tracks each button's icon ImageView + source file so focus can swap the icon to its
        // dark (black) variant when the button inverts to a white pill (mirrors the details
        // screen's ApplyActionButtonIconState / ResolveActionIconPath behavior).
        private readonly Dictionary<View, (ImageView Icon, string IconFile)> _buttonIcons = new();
        private int _buttonIndex;

        // Static backdrop/poster image shown as a placeholder before playback starts. Held
        // as a field so RevealVideo() can fade it out once the video is actually playing on
        // the plane behind the overlay. Null when the item has no backdrop image.
        private ImageView _backdrop;
        private bool _videoRevealed;

        // Invoked when the user activates Play/Resume (Enter) while the overlay is visible.
        public Action PlayRequested;
        // Invoked when the user presses Back while the overlay is visible.
        public Action BackRequested;

        public PreviewOverlay(JellyfinMovie item)
        {
            WidthResizePolicy = ResizePolicyType.FillToParent;
            HeightResizePolicy = ResizePolicyType.FillToParent;

            var apiKey = Uri.EscapeDataString(AppState.AccessToken);
            var serverUrl = AppState.Jellyfin.ServerUrl;
            var backdropUrl = JellyfinImageUrlBuilder.BuildBackdropUrl(
                item,
                serverUrl,
                apiKey,
                fallbackBackdropItemId: item != null && item.IsEpisode ? item.SeriesId : null);
            bool hasBackdropImage = !string.IsNullOrWhiteSpace(backdropUrl);

            var backdrop = new ImageView
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                PreMultipliedAlpha = false
            };
            UiAnimator.FadeInOnImageReady(backdrop, backdropUrl, UiAnimator.BackdropFadeInDurationMs);
            _backdrop = hasBackdropImage ? backdrop : null;

            var dim = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                BackgroundColor = hasBackdropImage ? UiTheme.DetailsBackdropDim : new Color(0f, 0f, 0f, 0.85f)
            };

            var content = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                Padding = new Extents(90, 90, 80, 80),
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    CellPadding = new Size2D(0, 26)
                }
            };

            content.Add(CreateTitleView(item));
            content.Add(CreateMetadataRow(item));
            content.Add(CreateSynopsisView(item));
            content.Add(CreateButtonRow(item));

            Add(backdrop);
            Add(dim);
            Add(content);

            FocusDefault();
        }

        private static View CreateTitleView(JellyfinMovie item)
        {
            // Logo for movies/series with a logo image; textual title otherwise (episodes
            // always use text — the SxxExx title built by DetailsScreenHelpers).
            if (item != null && !item.IsEpisode && item.HasLogo)
            {
                var logoUrl = AppState.GetItemLogoUrl(item.Id, 720, 50);
                if (!string.IsNullOrWhiteSpace(logoUrl))
                {
                    var logoContainer = new View
                    {
                        WidthResizePolicy = ResizePolicyType.FillToParent,
                        HeightSpecification = 136,
                        ClippingMode = ClippingModeType.ClipChildren,
                        Layout = new LinearLayout
                        {
                            LinearOrientation = LinearLayout.Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Begin
                        }
                    };
                    var logo = new ImageView
                    {
                        WidthSpecification = 720,
                        HeightSpecification = 136,
                        PreMultipliedAlpha = false,
                        FittingMode = FittingModeType.ShrinkToFit,
                        SamplingMode = SamplingModeType.Linear
                    };
                    UiAnimator.FadeInOnImageReady(logo, logoUrl, UiAnimator.BackdropFadeInDurationMs);
                    logoContainer.Add(logo);
                    return logoContainer;
                }
            }

            var titleText = item != null && item.IsEpisode
                ? DetailsScreenHelpers.BuildEpisodeTitle(item)
                : item?.Name ?? string.Empty;
            return new TextLabel(titleText)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                PointSize = 56,
                TextColor = Color.White,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word,
                Ellipsis = false,
                VerticalAlignment = VerticalAlignment.Top
            };
        }

        private static View CreateSynopsisView(JellyfinMovie item)
        {
            return new TextLabel(string.IsNullOrEmpty(item?.Overview) ? string.Empty : item.Overview)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 180,
                PointSize = 28,
                TextColor = UiTheme.DetailsOverviewText,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word,
                Ellipsis = true,
                VerticalAlignment = VerticalAlignment.Top
            };
        }

        // Metadata summary row matching the details screen: optional watched check, the
        // year · runtime · official-rating summary (shared DetailsScreenHelpers.BuildSummaryText),
        // and the community-rating ★ value. Built once from the fixed JellyfinMovie fields
        // (no server refresh needed here). Technical-spec chips (4K/HDR/codec) are intentionally
        // omitted — they need MediaSourceInfo that the overlay does not have at this point.
        private static View CreateMetadataRow(JellyfinMovie item)
        {
            var row = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 40,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    CellPadding = new Size2D(18, 0)
                }
            };

            if (item != null && item.Played)
            {
                row.Add(new ImageView
                {
                    WidthSpecification = 30,
                    HeightSpecification = 30,
                    ResourceUrl = IOPath.Combine(Tizen.Applications.Application.Current.DirectoryInfo.SharedResource, "check_circle.svg"),
                    PreMultipliedAlpha = false,
                    FittingMode = FittingModeType.ShrinkToFit,
                    SamplingMode = SamplingModeType.BoxThenLanczos
                });
            }

            var summaryText = DetailsScreenHelpers.BuildSummaryText(item);
            row.Add(new TextLabel(string.IsNullOrWhiteSpace(summaryText) ? " " : summaryText)
            {
                WidthResizePolicy = ResizePolicyType.UseNaturalSize,
                HeightResizePolicy = ResizePolicyType.UseNaturalSize,
                PointSize = 28,
                TextColor = new Color(0.88f, 0.88f, 0.88f, 1f),
                VerticalAlignment = VerticalAlignment.Center
            });

            if (item != null && item.CommunityRating.HasValue && item.CommunityRating.Value > 0)
            {
                var ratingGroup = new View
                {
                    WidthResizePolicy = ResizePolicyType.FitToChildren,
                    HeightResizePolicy = ResizePolicyType.FitToChildren,
                    Layout = new LinearLayout
                    {
                        LinearOrientation = LinearLayout.Orientation.Horizontal,
                        CellPadding = new Size2D(8, 0)
                    }
                };
                ratingGroup.Add(new TextLabel("★")
                {
                    WidthResizePolicy = ResizePolicyType.UseNaturalSize,
                    HeightResizePolicy = ResizePolicyType.UseNaturalSize,
                    PointSize = 32,
                    TextColor = new Color(0.95f, 0.78f, 0.29f, 1f),
                    VerticalAlignment = VerticalAlignment.Center
                });
                ratingGroup.Add(new TextLabel(item.CommunityRating.Value.ToString("0.0", CultureInfo.InvariantCulture))
                {
                    WidthResizePolicy = ResizePolicyType.UseNaturalSize,
                    HeightResizePolicy = ResizePolicyType.UseNaturalSize,
                    PointSize = 28,
                    TextColor = new Color(0.92f, 0.92f, 0.92f, 1f),
                    VerticalAlignment = VerticalAlignment.Center
                });
                row.Add(ratingGroup);
            }

            return row;
        }

        private View CreateButtonRow(JellyfinMovie item)
        {
            var buttonRow = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 100,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    CellPadding = new Size2D(28, 0)
                }
            };

            var playButton = CreateButton("Play", "play.svg");
            _buttons.Add(playButton);
            buttonRow.Add(playButton);

            bool resumeAvailable = item != null && item.PlaybackPositionTicks > 0;
            if (resumeAvailable)
            {
                var resumeButton = CreateButton("Resume", "resume.svg");
                _buttons.Add(resumeButton);
                buttonRow.Add(resumeButton);
            }

            return buttonRow;
        }

        // Icon + label pill button matching the details-screen action buttons: black pill with
        // white border/label + white icon when unfocused, inverting to a white pill with black
        // label/icon when focused (SetButtonFocusState handles bg+label; the icon swap is done
        // in ApplyButtonFocus via the dark-variant path).
        private View CreateButton(string text, string iconFile)
        {
            var button = new View
            {
                HeightSpecification = ActionButtonHeight,
                WidthResizePolicy = ResizePolicyType.FitToChildren,
                Padding = new Extents(34, 34, 8, 8),
                Focusable = true,
                BackgroundColor = Color.Black,
                CornerRadius = ActionButtonHeight / 2.0f,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BorderlineWidth = 2.0f,
                BorderlineColor = Color.White,
                ClippingMode = ClippingModeType.ClipChildren,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    CellPadding = new Size2D(ButtonIconLabelGap, 0)
                }
            };
            var icon = new ImageView
            {
                WidthSpecification = ButtonIconSize,
                HeightSpecification = ButtonIconSize,
                ResourceUrl = ResolveActionIconPath(iconFile, focused: false),
                PreMultipliedAlpha = false,
                FittingMode = FittingModeType.ShrinkToFit,
                SamplingMode = SamplingModeType.BoxThenLanczos
            };
            var label = new TextLabel(text)
            {
                WidthResizePolicy = ResizePolicyType.FitToChildren,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                PointSize = 26,
                TextColor = Color.White,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            button.Add(icon);
            button.Add(label);
            _buttonIcons[button] = (icon, iconFile);
            UiFactory.SetButtonFocusState(button, focused: false);
            return button;
        }

        // Returns the white source SVG path when unfocused, or a cached black-recolored variant
        // when focused (so the icon stays visible on the white focused pill). Shares the same
        // "icon-cache" directory + naming as the details screen, so variants are reused.
        private static string ResolveActionIconPath(string iconFile, bool focused)
        {
            string source = IOPath.Combine(Tizen.Applications.Application.Current.DirectoryInfo.SharedResource, iconFile);
            if (!focused || !iconFile.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) || !File.Exists(source))
                return source;

            try
            {
                string versionToken = File.GetLastWriteTimeUtc(source).Ticks.ToString(CultureInfo.InvariantCulture);
                string cacheDir = IOPath.Combine(Tizen.Applications.Application.Current.DirectoryInfo.Data, "icon-cache");
                Directory.CreateDirectory(cacheDir);
                string baseName = IOPath.GetFileNameWithoutExtension(iconFile);
                string darkPath = IOPath.Combine(cacheDir, $"{baseName}_dark_{versionToken}.svg");
                if (!File.Exists(darkPath))
                {
                    string svg = File.ReadAllText(source);
                    string darkSvg = svg
                        .Replace("#FFFFFF", "#000000", StringComparison.OrdinalIgnoreCase)
                        .Replace("fill=\"white\"", "fill=\"#000000\"", StringComparison.OrdinalIgnoreCase)
                        .Replace("stroke=\"#FFFFFF\"", "stroke=\"#000000\"", StringComparison.OrdinalIgnoreCase)
                        .Replace("stroke=\"white\"", "stroke=\"#000000\"", StringComparison.OrdinalIgnoreCase);
                    File.WriteAllText(darkPath, darkSvg);
                }
                return darkPath;
            }
            catch
            {
                return source;
            }
        }

        // Fades the static backdrop placeholder out so the video playing on the plane behind
        // the overlay becomes visible; the dim layer and content (title/synopsis/buttons) stay
        // on top. Safe no-op when there is no backdrop image or it was already revealed. Cancels
        // the pending FadeInOnImageReady handler first, so a late-loading image cannot animate
        // the backdrop back to opaque after this fade-out.
        public void RevealVideo()
        {
            if (_backdrop == null || _videoRevealed)
                return;
            _videoRevealed = true;
            UiAnimator.CancelFadeIn(_backdrop);
            UiAnimator.AnimateTo(_backdrop, "Opacity", 0.0f, UiAnimator.BackdropFadeInDurationMs);
        }

        // Resets focus to the first (primary) button. Called on creation and whenever the
        // overlay is re-shown.
        public void FocusDefault()
        {
            _buttonIndex = 0;
            ApplyButtonFocus();
        }

        // Driven by VideoPlayerScreen.HandleKey. Play/Resume both raise PlayRequested (the
        // start position was already resolved once via ResolveStartPositionMs), Back raises
        // BackRequested.
        public void HandleKey(AppKey key)
        {
            switch (key)
            {
                case AppKey.Left: MoveFocus(-1); break;
                case AppKey.Right: MoveFocus(1); break;
                case AppKey.Enter: PlayRequested?.Invoke(); break;
                case AppKey.Back: BackRequested?.Invoke(); break;
            }
        }

        private void MoveFocus(int delta)
        {
            if (_buttons.Count == 0) return;
            _buttonIndex = Math.Clamp(_buttonIndex + delta, 0, _buttons.Count - 1);
            ApplyButtonFocus();
        }

        private void ApplyButtonFocus()
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                bool focused = i == _buttonIndex;
                var button = _buttons[i];
                UiFactory.SetButtonFocusState(button, focused);
                if (_buttonIcons.TryGetValue(button, out var iconInfo))
                    iconInfo.Icon.ResourceUrl = ResolveActionIconPath(iconInfo.IconFile, focused);
                button.Scale = focused
                    ? new Vector3(ButtonFocusScale, ButtonFocusScale, 1f)
                    : Vector3.One;
            }
        }
    }
}
