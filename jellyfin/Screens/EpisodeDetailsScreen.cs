using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using Tizen.NUI.Components;
using JellyfinTizen.Core;
using JellyfinTizen.Models;
using JellyfinTizen.UI;
using JellyfinTizen.Utils;

namespace JellyfinTizen.Screens
{
    public class EpisodeDetailsScreen : ScreenBase, IKeyHandler
    {
        private enum DetailsPanelMode
        {
            None,
            Subtitle,
            Audio
        }

        private const float ButtonFocusScale = 1.08f;
        private const int FixedTopContentHeight = 500;
        private const int FixedOverviewViewportHeight = 240;
        private const int OverviewScrollStepPx = 70;
        private const int OverviewScrollTailPx = 28;
        private const int ActionButtonHeight = 70;
        private const int ActionButtonRowGap = 28;
        private const int SecondaryActionButtonWidth = 240;
        private const int PlayActionButtonWidth = 176;
        private const int IconActionButtonWidth = 122;
        private const int ActionButtonIconLabelGap = 6;
        private const int PlayActionButtonIconSize = 46;
        private const int AudioActionButtonIconSize = 36;
        private const int SubtitleActionButtonIconSize = 34;
        private const int DetailsHorizontalPadding = 90;
        private const int DetailsColumnGap = 60;
        private const int EpisodeThumbWidth = 640;
        private const int EpisodeThumbHeight = 360;
        private const float SeriesTitlePointSize = 28f;
        private readonly JellyfinMovie _episode;
        private readonly bool _resumeAvailable;
        private View _playButton;
        private View _resumeButton;
        private View _audioButton;
        private View _subtitleButton;
        private View _versionButton;
        private readonly List<View> _buttons = new();
        private int _buttonIndex;
        private View _infoColumn;
        private View _buttonGroup;
        private View _buttonRowTop;
        private View _buttonRowBottom;
        
        private List<MediaStream> _subtitleStreams;
        private List<MediaSourceInfo> _mediaSources = new();
        private int _selectedMediaSourceIndex = 0;
        private int? _selectedSubtitleIndex = null;
        private int? _selectedAudioIndex = null;
        private bool _subtitleStreamsLoaded;
        private bool _mediaSourcesLoaded;
        private readonly Dictionary<View, Animation> _focusAnimations = new();
        private readonly Dictionary<string, string> _darkButtonIconPathCache = new(StringComparer.OrdinalIgnoreCase);
        private DetailsSelectionPanel _selectionPanel;
        private DetailsPanelMode _selectionPanelMode;
        private View _metadataContainer;
        private View _metadataSummaryRow;
        private TextLabel _metadataSummaryLabel;
        private View _metadataRatingGroup;
        private TextLabel _metadataRatingLabel;
        private View _metadataTagRow;
        private View _overviewViewport;
        private TextLabel _overviewLabel;
        private int _overviewScrollOffset;
        private int _overviewMaxScroll;
        private bool _actionButtonReflowScheduled;
        private const string DolbyAudioChipPrefix = "__DOLBY_AUDIO__:";
        private const string DolbyVisionChipToken = "__DOLBY_VISION_ICON__";
        public EpisodeDetailsScreen(
            JellyfinMovie episode,
            List<MediaStream> prefetchedSubtitleStreams = null,
            List<MediaSourceInfo> prefetchedMediaSources = null)
        {
            _episode = episode;
            _resumeAvailable = episode.PlaybackPositionTicks > 0;
            if (prefetchedSubtitleStreams != null)
                _subtitleStreams = prefetchedSubtitleStreams;
            if (prefetchedMediaSources != null)
                _mediaSources = prefetchedMediaSources;
            _subtitleStreamsLoaded = prefetchedSubtitleStreams != null;
            _mediaSourcesLoaded = prefetchedMediaSources != null;
            var root = UiFactory.CreateAtmosphericBackground();
            var apiKey = Uri.EscapeDataString(AppState.AccessToken);
            var serverUrl = AppState.Jellyfin.ServerUrl;
            var backdropItemId =
                _episode.IsEpisode && !string.IsNullOrWhiteSpace(_episode.SeriesId)
                    ? _episode.SeriesId
                    : _episode.Id;
            var backdropUrl = JellyfinImageUrlBuilder.BuildBackdropUrl(
                _episode,
                serverUrl,
                apiKey,
                maxWidth: 1920,
                fallbackBackdropItemId: backdropItemId != _episode.Id ? backdropItemId : null);
            bool hasBackdropImage = !string.IsNullOrWhiteSpace(backdropUrl);
            var backdrop = new ImageView
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                ResourceUrl = backdropUrl,
                PreMultipliedAlpha = false
            };
            var dimOverlay = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                BackgroundColor = hasBackdropImage ? UiTheme.DetailsBackdropDim : Color.Transparent
            };
            var content = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                Padding = new Extents(90, 90, 80, 80),
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    CellPadding = new Size2D(60, 0)
                }
            };
            
            var thumbUrl =
                _episode.HasThumb
                    ? $"{serverUrl}/Items/{_episode.Id}/Images/Thumb/0?maxWidth={EpisodeThumbWidth}&quality=95&api_key={apiKey}"
                    : _episode.HasBackdrop
                        ? $"{serverUrl}/Items/{_episode.Id}/Images/Backdrop/0?maxWidth={EpisodeThumbWidth}&quality=90&api_key={apiKey}"
                        : $"{serverUrl}/Items/{_episode.Id}/Images/Primary/0?maxWidth={EpisodeThumbWidth}&quality=95&api_key={apiKey}";
            
            var thumbFrame = new View
            {
                WidthSpecification = EpisodeThumbWidth,
                HeightSpecification = EpisodeThumbHeight,
                BackgroundColor = UiTheme.DetailsPosterSurface,
                CornerRadius = 16.0f,
                ClippingMode = ClippingModeType.ClipChildren
            };
            
            var thumb = new ImageView
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                ResourceUrl = thumbUrl,
                PreMultipliedAlpha = false
            };
            thumbFrame.Add(thumb);
            
            _infoColumn = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent
            };

            var topContentViewport = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = FixedTopContentHeight,
                ClippingMode = ClippingModeType.ClipChildren
            };

            var topContent = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    CellPadding = new Size2D(0, 26)
                }
            };
            var seriesTitleText = BuildSeriesTitle(_episode);
            TextLabel seriesTitle = null;
            if (!string.IsNullOrWhiteSpace(seriesTitleText))
            {
                seriesTitle = new TextLabel(seriesTitleText)
                {
                    WidthResizePolicy = ResizePolicyType.FillToParent,
                    HeightResizePolicy = ResizePolicyType.FitToChildren,
                    PointSize = SeriesTitlePointSize,
                    TextColor = new Color(1f, 1f, 1f, 0.74f),
                    MultiLine = false,
                    Ellipsis = true,
                    VerticalAlignment = VerticalAlignment.Top
                };
            }

            var titleText = BuildEpisodeTitle(_episode);
            var titlePointSize = GetAdaptiveTitlePointSize(titleText);
            var title = new TextLabel(titleText)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                PointSize = titlePointSize,
                TextColor = Color.White,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word,
                Ellipsis = false,
                VerticalAlignment = VerticalAlignment.Top
            };

            _metadataContainer = CreateMetadataView();
            var overviewText = string.IsNullOrEmpty(_episode.Overview)
                ? "No overview available."
                : _episode.Overview;
            var overviewPointSize = 31f;
            _overviewViewport = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = FixedOverviewViewportHeight,
                ClippingMode = ClippingModeType.ClipChildren
            };

            _overviewLabel = new TextLabel(overviewText)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                PointSize = overviewPointSize,
                TextColor = UiTheme.DetailsOverviewText,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word,
                Ellipsis = false,
                VerticalAlignment = VerticalAlignment.Top
            };

            _overviewViewport.Add(_overviewLabel);
            if (seriesTitle != null)
                topContent.Add(seriesTitle);
            topContent.Add(title);
            topContent.Add(_metadataContainer);
            topContent.Add(_overviewViewport);
            topContentViewport.Add(topContent);
            _infoColumn.Add(topContentViewport);
            UpdateMetadataView();
            _buttonGroup = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    CellPadding = new Size2D(0, 14)
                },
                Margin = new Extents(0, 0, 26, 0),
                PositionY = 560
            };

            _buttonRowTop = CreateButtonRow();
            _buttonRowBottom = CreateButtonRow();
            _buttonGroup.Add(_buttonRowTop);
            _buttonGroup.Add(_buttonRowBottom);

            _playButton = CreateActionButton("Play", isPrimary: true, iconFile: "play.svg", width: PlayActionButtonWidth, iconSize: PlayActionButtonIconSize);

            if (_resumeAvailable)
            {
                _resumeButton = CreateActionButton("Resume", isPrimary: false, iconFile: "resume.svg", width: null, iconSize: PlayActionButtonIconSize);
            }

            _audioButton = CreateActionButton(string.Empty, isPrimary: false, iconFile: "audio.svg", width: IconActionButtonWidth, iconSize: AudioActionButtonIconSize);
            _subtitleButton = CreateActionButton(string.Empty, isPrimary: false, iconFile: "sub.svg", width: IconActionButtonWidth, iconSize: SubtitleActionButtonIconSize);

            _versionButton = CreateActionButton("Default", isPrimary: false);
            RebuildActionButtons(includeVersionButton: _mediaSources.Count > 1);
            UpdateVersionButtonText();
            NormalizeSelectionStateForCurrentMediaSource();
            UpdateMetadataView();

            _infoColumn.Add(_buttonGroup);
            content.Add(thumbFrame);
            content.Add(_infoColumn);
            root.Add(backdrop);
            root.Add(dimOverlay);
            root.Add(content);
            Add(root);
            _selectionPanel = new DetailsSelectionPanel(this);
        }

        private static float GetAdaptiveTitlePointSize(string titleText)
        {
            int length = string.IsNullOrWhiteSpace(titleText) ? 0 : titleText.Length;
            if (length > 90) return 46f;
            if (length > 65) return 50f;
            return 56f;
        }

        private static string BuildSeriesTitle(JellyfinMovie episode)
        {
            if (episode == null)
                return string.Empty;
            if (!episode.IsEpisode)
                return string.Empty;

            return string.IsNullOrWhiteSpace(episode.SeriesName)
                ? string.Empty
                : episode.SeriesName.Trim();
        }

        private static string BuildEpisodeTitle(JellyfinMovie episode)
        {
            if (episode == null)
                return string.Empty;

            var episodeCode = GetEpisodeCode(episode);
            if (string.IsNullOrWhiteSpace(episodeCode))
                return episode.Name ?? string.Empty;

            if (string.IsNullOrWhiteSpace(episode.Name))
                return episodeCode;

            return $"{episodeCode} - {episode.Name}";
        }

        private static string GetEpisodeCode(JellyfinMovie episode)
        {
            if (episode == null)
                return string.Empty;

            if (episode.ParentIndexNumber > 0 && episode.IndexNumber > 0)
                return $"S{episode.ParentIndexNumber}:E{episode.IndexNumber}";

            if (episode.IndexNumber > 0)
                return $"E{episode.IndexNumber}";

            return string.Empty;
        }

        public override void OnShow()
        {
            if (!_subtitleStreamsLoaded)
                _ = LoadSubtitleStreamsAsync();
            if (!_mediaSourcesLoaded)
                _ = LoadMediaSourcesAsync();
            FocusButton(0);
            RunOnUiThread(RefreshOverviewScrollBounds);
            ScheduleActionButtonReflow();
        }

        public override void OnHide()
        {
            UiAnimator.StopAndDisposeAll(_focusAnimations);
            HideSelectionPanel();
        }

        private async Task LoadSubtitleStreamsAsync()
        {
            if (_subtitleStreamsLoaded)
                return;

            try
            {
                _subtitleStreams = await AppState.Jellyfin.GetSubtitleStreamsAsync(_episode.Id);
            }
            catch
            {
                // Ignore
            }

            _subtitleStreamsLoaded = true;
            NormalizeSelectionStateForCurrentMediaSource();
        }

        private async Task LoadMediaSourcesAsync()
        {
            if (_mediaSourcesLoaded)
                return;

            try
            {
                var playbackInfo = await AppState.Jellyfin.GetPlaybackInfoAsync(_episode.Id, subtitleHandlingDisabled: true);
                _mediaSources = playbackInfo?.MediaSources ?? new List<MediaSourceInfo>();
            }
            catch
            {
                _mediaSources = new List<MediaSourceInfo>();
            }

            if (_mediaSources.Count == 0)
            {
                _mediaSources.Add(new MediaSourceInfo
                {
                    Id = _episode.Id,
                    Name = "Default"
                });
            }

            _mediaSourcesLoaded = true;
            _selectedMediaSourceIndex = Math.Clamp(_selectedMediaSourceIndex, 0, _mediaSources.Count - 1);
            NormalizeSelectionStateForCurrentMediaSource();
            RebuildActionButtons(includeVersionButton: _mediaSources.Count > 1);
            UpdateVersionButtonText();
            UpdateMetadataView();
            ScheduleActionButtonReflow();

            if (_buttons.Count > 0)
                FocusButton(_buttonIndex);
        }

        private void ScheduleActionButtonReflow()
        {
            if (_actionButtonReflowScheduled || _buttonGroup == null)
                return;

            _actionButtonReflowScheduled = true;
            RunOnUiThread(() =>
            {
                RunOnUiThread(() =>
                {
                    _actionButtonReflowScheduled = false;
                    if (_buttonGroup == null)
                        return;

                    RebuildActionButtons(includeVersionButton: _mediaSources.Count > 1);
                    if (_buttons.Count > 0)
                        FocusButton(_buttonIndex);
                });
            });
        }

        public void HandleKey(AppKey key)
        {
            if (HandleSelectionPanelKey(key))
                return;

            switch (key)
            {
                case AppKey.Up:
                    ScrollOverview(-OverviewScrollStepPx);
                    break;
                case AppKey.Down:
                    ScrollOverview(OverviewScrollStepPx);
                    break;
                case AppKey.Left:
                    MoveFocus(-1);
                    break;
                case AppKey.Right:
                    MoveFocus(1);
                    break;
                case AppKey.Enter:
                    ActivateFocusedButton();
                    break;
                case AppKey.Back:
                    NavigationService.NavigateBack();
                    break;
            }
        }
        private View CreateActionButton(string text, bool isPrimary, string iconFile = null, int? width = SecondaryActionButtonWidth, int iconSize = 30)
        {
            var button = new View
            {
                HeightSpecification = ActionButtonHeight,
                BackgroundColor = Color.Black,
                Focusable = true,
                CornerRadius = ActionButtonHeight / 2.0f,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BorderlineWidth = 2.0f,
                BorderlineColor = Color.White,
                ClippingMode = ClippingModeType.ClipChildren
            };
            bool autoWidth = !width.HasValue;
            if (autoWidth)
            {
                button.WidthResizePolicy = ResizePolicyType.FitToChildren;
                button.Padding = new Extents(34, 34, 8, 8);
                button.Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            else
            {
                button.WidthSpecification = width.Value;
            }
            if (!string.IsNullOrWhiteSpace(iconFile))
            {
                var icon = new ImageView
                {
                    WidthSpecification = iconSize,
                    HeightSpecification = iconSize,
                    ResourceUrl = ResolveActionIconPath(iconFile, focused: false),
                    Name = iconFile,
                    PreMultipliedAlpha = false,
                    FittingMode = FittingModeType.ShrinkToFit,
                    SamplingMode = SamplingModeType.BoxThenLanczos,
                    ParentOrigin = Tizen.NUI.ParentOrigin.Center,
                    PivotPoint = Tizen.NUI.PivotPoint.Center,
                    PositionUsesPivotPoint = true
                };

                if (!string.IsNullOrWhiteSpace(text))
                {
                    var content = new View
                    {
                        WidthResizePolicy = ResizePolicyType.FitToChildren,
                        HeightResizePolicy = ResizePolicyType.FitToChildren,
                        Layout = new LinearLayout
                        {
                            LinearOrientation = LinearLayout.Orientation.Horizontal,
                            CellPadding = new Size2D(ActionButtonIconLabelGap, 0)
                        }
                    };
                    if (!autoWidth)
                    {
                        content.ParentOrigin = Tizen.NUI.ParentOrigin.Center;
                        content.PivotPoint = Tizen.NUI.PivotPoint.Center;
                        content.PositionUsesPivotPoint = true;
                    }

                    var label = new TextLabel(text)
                    {
                        HeightSpecification = iconSize,
                        WidthResizePolicy = ResizePolicyType.FitToChildren,
                        VerticalAlignment = VerticalAlignment.Center,
                        PointSize = 26,
                        TextColor = Color.White,
                        Ellipsis = true
                    };

                    var rowIcon = new ImageView
                    {
                        WidthSpecification = iconSize,
                        HeightSpecification = iconSize,
                        ResourceUrl = ResolveActionIconPath(iconFile, focused: false),
                        Name = iconFile,
                        PreMultipliedAlpha = false,
                        FittingMode = FittingModeType.ShrinkToFit,
                        SamplingMode = SamplingModeType.BoxThenLanczos
                    };

                    content.Add(rowIcon);
                    content.Add(label);
                    button.Add(content);
                }
                else
                {
                    button.Add(icon);
                }
            }
            else
            {
                var label = new TextLabel(text)
                {
                    WidthResizePolicy = ResizePolicyType.FillToParent,
                    HeightResizePolicy = ResizePolicyType.FillToParent,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    PointSize = 26,
                    TextColor = Color.White,
                    Ellipsis = true,
                    Padding = new Extents(24, 24, 0, 0)
                };
                button.Add(label);
            }
            ApplyActionButtonVisual(button, focused: false);
            return button;
        }

        private void ApplyActionButtonVisual(View button, bool focused)
        {
            if (button == null)
                return;

            UiFactory.SetButtonFocusState(button, focused: focused);
            ApplyActionButtonIconState(button, focused);
        }

        private void ApplyActionButtonIconState(View view, bool focused)
        {
            if (view == null)
                return;

            if (view is ImageView icon && !string.IsNullOrWhiteSpace(icon.Name))
                icon.ResourceUrl = ResolveActionIconPath(icon.Name, focused);

            uint childCount = view.ChildCount;
            for (uint i = 0; i < childCount; i++)
            {
                if (view.GetChildAt(i) is View child)
                    ApplyActionButtonIconState(child, focused);
            }
        }

        private string ResolveActionIconPath(string iconFile, bool focused)
        {
            if (string.IsNullOrWhiteSpace(iconFile))
                return string.Empty;

            string sourcePath = System.IO.Path.Combine(Tizen.Applications.Application.Current.DirectoryInfo.SharedResource, iconFile);
            if (!focused ||
                !iconFile.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(sourcePath))
            {
                return sourcePath;
            }

            string versionToken;
            try
            {
                versionToken = File.GetLastWriteTimeUtc(sourcePath)
                    .Ticks
                    .ToString(CultureInfo.InvariantCulture);
            }
            catch
            {
                versionToken = "0";
            }

            string cacheKey = $"{sourcePath}|{versionToken}";
            if (_darkButtonIconPathCache.TryGetValue(cacheKey, out var cachedPath) && File.Exists(cachedPath))
                return cachedPath;

            try
            {
                string svg = File.ReadAllText(sourcePath);
                string darkSvg = svg
                    .Replace("#FFFFFF", "#000000", StringComparison.OrdinalIgnoreCase)
                    .Replace("fill=\"white\"", "fill=\"#000000\"", StringComparison.OrdinalIgnoreCase)
                    .Replace("stroke=\"#FFFFFF\"", "stroke=\"#000000\"", StringComparison.OrdinalIgnoreCase)
                    .Replace("stroke=\"white\"", "stroke=\"#000000\"", StringComparison.OrdinalIgnoreCase);

                string cacheDir = System.IO.Path.Combine(Tizen.Applications.Application.Current.DirectoryInfo.Data, "icon-cache");
                Directory.CreateDirectory(cacheDir);

                string baseName = System.IO.Path.GetFileNameWithoutExtension(iconFile);
                string darkPath = System.IO.Path.Combine(cacheDir, $"{baseName}_dark_{versionToken}.svg");
                if (!File.Exists(darkPath))
                    File.WriteAllText(darkPath, darkSvg);

                _darkButtonIconPathCache[cacheKey] = darkPath;
                return darkPath;
            }
            catch
            {
                return sourcePath;
            }
        }

        private static View CreateButtonRow()
        {
            return new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 100,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    CellPadding = new Size2D(42, 0)
                }
            };
        }

        private void RebuildActionButtons(bool includeVersionButton)
        {
            if (_buttonGroup == null || _buttonRowTop == null || _buttonRowBottom == null)
                return;

            ClearRowChildren(_buttonRowTop);
            ClearRowChildren(_buttonRowBottom);
            _buttons.Clear();
            int topRowAvailableWidth = ResolveTopActionRowAvailableWidth();
            int topRowUsedWidth = 0;
            bool wrappedToSecondRow = false;

            AddActionButton(_playButton);
            if (_resumeButton != null)
                AddActionButton(_resumeButton);
            AddActionButton(_audioButton);
            AddActionButton(_subtitleButton);
            if (includeVersionButton)
                AddActionButton(_versionButton);

            _buttonIndex = Math.Clamp(_buttonIndex, 0, Math.Max(0, _buttons.Count - 1));

            void AddActionButton(View button)
            {
                if (button == null)
                    return;

                _buttons.Add(button);
                if (wrappedToSecondRow)
                {
                    _buttonRowBottom.Add(button);
                    return;
                }

                int buttonWidth = EstimateActionButtonWidth(button);
                int gapBefore = _buttonRowTop.ChildCount > 0 ? ActionButtonRowGap : 0;
                bool fitsTopRow = _buttonRowTop.ChildCount == 0 || (topRowUsedWidth + gapBefore + buttonWidth) <= topRowAvailableWidth;
                if (fitsTopRow)
                {
                    _buttonRowTop.Add(button);
                    topRowUsedWidth += gapBefore + buttonWidth;
                    return;
                }

                wrappedToSecondRow = true;
                _buttonRowBottom.Add(button);
            }
        }

        private int ResolveTopActionRowAvailableWidth()
        {
            int groupWidth = (int)Math.Round(_buttonGroup?.SizeWidth ?? 0);
            int infoWidth = (int)Math.Round(_infoColumn?.SizeWidth ?? 0);
            int fallbackWidth = Math.Max(320, Window.Default.Size.Width - (DetailsHorizontalPadding * 2) - EpisodeThumbWidth - DetailsColumnGap);
            return Math.Max(fallbackWidth, Math.Max(infoWidth, groupWidth));
        }

        private static int EstimateActionButtonWidth(View button)
        {
            if (button == null)
                return 0;

            int actualWidth = (int)Math.Round(button.SizeWidth);
            if (actualWidth > 0)
                return actualWidth;

            int specifiedWidth = (int)Math.Round((double)(float)button.WidthSpecification);
            if (specifiedWidth > 0)
                return specifiedWidth;

            string buttonText = FindActionButtonText(button);
            bool hasIcon = ContainsActionButtonIcon(button);
            int iconWidth = hasIcon ? 46 : 0;
            int textWidth = EstimateActionButtonTextWidth(buttonText);
            int contentGap = hasIcon && !string.IsNullOrWhiteSpace(buttonText) ? 14 : 0;
            int paddingWidth = 68;
            int estimatedWidth = paddingWidth + iconWidth + contentGap + textWidth;
            return Math.Clamp(estimatedWidth, 180, 620);
        }

        private static int EstimateActionButtonTextWidth(string text)
        {
            int length = string.IsNullOrWhiteSpace(text) ? 0 : text.Trim().Length;
            if (length <= 0)
                return 0;

            return Math.Clamp(40 + (length * 15), 80, 360);
        }

        private static string FindActionButtonText(View view)
        {
            if (view == null)
                return null;

            if (view is TextLabel label && !string.IsNullOrWhiteSpace(label.Text))
                return label.Text;

            uint childCount = view.ChildCount;
            for (uint i = 0; i < childCount; i++)
            {
                if (view.GetChildAt(i) is View child)
                {
                    string childText = FindActionButtonText(child);
                    if (!string.IsNullOrWhiteSpace(childText))
                        return childText;
                }
            }

            return null;
        }

        private static bool ContainsActionButtonIcon(View view)
        {
            if (view == null)
                return false;

            if (view is ImageView)
                return true;

            uint childCount = view.ChildCount;
            for (uint i = 0; i < childCount; i++)
            {
                if (view.GetChildAt(i) is View child && ContainsActionButtonIcon(child))
                    return true;
            }

            return false;
        }

        private void RefreshOverviewScrollBounds()
        {
            if (_overviewViewport == null || _overviewLabel == null)
                return;

            int viewportHeight = (int)Math.Round(_overviewViewport.SizeHeight);
            if (viewportHeight <= 0)
                viewportHeight = FixedOverviewViewportHeight;

            int viewportWidth = (int)Math.Round(_overviewViewport.SizeWidth);
            int measuredHeight = (int)Math.Round(_overviewLabel.SizeHeight);
            int estimatedHeight = EstimateOverviewContentHeight(viewportWidth);
            int contentHeight = Math.Max(viewportHeight, Math.Max(measuredHeight, estimatedHeight));

            _overviewMaxScroll = Math.Max(0, contentHeight - viewportHeight + OverviewScrollTailPx);
            _overviewScrollOffset = Math.Clamp(_overviewScrollOffset, 0, _overviewMaxScroll);
            _overviewLabel.PositionY = -_overviewScrollOffset;
        }

        private void ScrollOverview(int delta)
        {
            if (_overviewLabel == null)
                return;

            // Recompute on each input so late layout updates can expand the reachable range.
            RefreshOverviewScrollBounds();

            int nextOffset = Math.Clamp(_overviewScrollOffset + delta, 0, _overviewMaxScroll);
            if (nextOffset == _overviewScrollOffset)
                return;

            _overviewScrollOffset = nextOffset;
            _overviewLabel.PositionY = -_overviewScrollOffset;
        }

        private int EstimateOverviewContentHeight(int viewportWidth)
        {
            string text = _overviewLabel?.Text ?? string.Empty;
            if (string.IsNullOrEmpty(text))
                return 0;

            float pointSize = _overviewLabel.PointSize > 0 ? _overviewLabel.PointSize : 31f;
            int safeWidth = viewportWidth > 0 ? viewportWidth : 960;
            int charsPerLine = Math.Max(12, (int)Math.Floor(safeWidth / Math.Max(1f, pointSize * 0.56f)));
            int lineCount = 0;

            foreach (var paragraph in text.Split('\n'))
            {
                if (paragraph.Length == 0)
                {
                    lineCount += 1;
                    continue;
                }

                lineCount += (int)Math.Ceiling(paragraph.Length / (double)charsPerLine);
            }

            int lineHeight = (int)Math.Ceiling(pointSize * 1.55f);
            return Math.Max(0, lineCount * lineHeight);
        }

        private static void ClearRowChildren(View row)
        {
            while (row != null && row.ChildCount > 0)
            {
                var child = row.GetChildAt(0);
                row.Remove(child);
            }
        }

        private static bool HasChild(View parent, View child)
        {
            if (parent == null || child == null)
                return false;

            foreach (var existing in parent.Children)
            {
                if (ReferenceEquals(existing, child))
                    return true;
            }

            return false;
        }
        private void MoveFocus(int delta)
        {
            if (_buttons.Count == 0)
                return;
            var newIndex = Math.Clamp(_buttonIndex + delta, 0, _buttons.Count - 1);
            FocusButton(newIndex);
        }
        private void FocusButton(int index)
        {
            _buttonIndex = Math.Clamp(index, 0, _buttons.Count - 1);
            for (int i = 0; i < _buttons.Count; i++)
            {
                var focused = i == _buttonIndex;
                var button = _buttons[i];
                AnimateScale(button, focused ? new Vector3(ButtonFocusScale, ButtonFocusScale, 1f) : Vector3.One);
                ApplyActionButtonVisual(button, focused);
            }

            if (_buttonIndex >= 0)
            {
                FocusManager.Instance.SetCurrentFocusView(_buttons[_buttonIndex]);
            }
        }

        private void AnimateScale(View view, Vector3 targetScale)
        {
            if (view == null)
                return;

            if (_focusAnimations.TryGetValue(view, out var existing))
            {
                UiAnimator.StopAndDispose(ref existing);
                _focusAnimations.Remove(view);
            }

            var animation = UiAnimator.Start(
                UiAnimator.FocusDurationMs,
                anim => anim.AnimateTo(view, "Scale", targetScale),
                () => _focusAnimations.Remove(view)
            );

            _focusAnimations[view] = animation;
        }
        private void ActivateFocusedButton()
        {
            if (_buttonIndex < 0 || _buttonIndex >= _buttons.Count)
                return;

            var focusedButton = _buttons[_buttonIndex];
            if (focusedButton == _playButton)
            {
                PlayMedia(_episode, 0);
                return;
            }
            if (_resumeButton != null && focusedButton == _resumeButton)
            {
                PlayMedia(_episode, TicksToMs(_episode.PlaybackPositionTicks));
                return;
            }
            if (focusedButton == _audioButton)
            {
                ShowAudioPanel();
                return;
            }
            if (focusedButton == _subtitleButton)
            {
                ShowSubtitlePanel();
                return;
            }
            if (focusedButton == _versionButton)
            {
                CycleMediaSource();
                return;
            }
        }

        private bool HandleSelectionPanelKey(AppKey key)
        {
            if (_selectionPanel == null || !_selectionPanel.IsVisible)
                return false;

            switch (key)
            {
                case AppKey.Up:
                    _selectionPanel.MoveSelection(-1);
                    break;
                case AppKey.Down:
                    _selectionPanel.MoveSelection(1);
                    break;
                case AppKey.Enter:
                    ApplyPanelSelection();
                    break;
                case AppKey.Back:
                    HideSelectionPanel();
                    break;
            }

            return true;
        }

        private void ShowSubtitlePanel()
        {
            if (_selectionPanel == null)
                return;

            var subtitleStreams = GetAvailableSubtitleStreams();
            var options = new List<DetailsSelectionOption>
            {
                new DetailsSelectionOption("OFF_INDEX", "OFF")
            };

            int selectedIndex = 0;
            foreach (var stream in subtitleStreams)
            {
                options.Add(new DetailsSelectionOption(stream.Index.ToString(CultureInfo.InvariantCulture), FormatSubtitleStreamOption(stream)));
                if (_selectedSubtitleIndex.HasValue && stream.Index == _selectedSubtitleIndex.Value)
                    selectedIndex = options.Count - 1;
            }

            _selectionPanelMode = DetailsPanelMode.Subtitle;
            _selectionPanel.Show("Subtitles", options, selectedIndex, UiTheme.PlayerOverlayItem);
        }

        private void ShowAudioPanel()
        {
            if (_selectionPanel == null)
                return;

            var audioStreams = GetAvailableAudioStreams();
            if (audioStreams.Count == 0)
                return;

            int? selectedAudioIndex = GetEffectiveSelectedAudioIndex(audioStreams);
            int selectedIndex = 0;
            var options = new List<DetailsSelectionOption>(audioStreams.Count);
            foreach (var stream in audioStreams)
            {
                options.Add(new DetailsSelectionOption(stream.Index.ToString(CultureInfo.InvariantCulture), FormatAudioStreamOption(stream)));
                if (selectedAudioIndex.HasValue && stream.Index == selectedAudioIndex.Value)
                    selectedIndex = options.Count - 1;
            }

            _selectionPanelMode = DetailsPanelMode.Audio;
            _selectionPanel.Show("Audio Tracks", options, selectedIndex, UiTheme.PlayerAudioItem);
        }

        private void ApplyPanelSelection()
        {
            if (_selectionPanel == null || !_selectionPanel.TryGetSelectedOption(out var selectedOption))
            {
                HideSelectionPanel();
                return;
            }

            switch (_selectionPanelMode)
            {
                case DetailsPanelMode.Subtitle:
                    _selectedSubtitleIndex = string.Equals(selectedOption.Id, "OFF_INDEX", StringComparison.Ordinal)
                        ? null
                        : int.TryParse(selectedOption.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var subtitleIndex)
                            ? subtitleIndex
                            : null;
                    UpdateSubtitleButtonText();
                    break;
                case DetailsPanelMode.Audio:
                    if (int.TryParse(selectedOption.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var audioIndex))
                    {
                        _selectedAudioIndex = audioIndex;
                        UpdateAudioButtonText();
                    }
                    break;
            }

            HideSelectionPanel();
        }

        private void HideSelectionPanel()
        {
            _selectionPanelMode = DetailsPanelMode.None;
            _selectionPanel?.Hide();
        }

        private void CycleMediaSource()
        {
            if (_mediaSources == null || _mediaSources.Count <= 1)
            {
                UpdateVersionButtonText();
                return;
            }

            _selectedMediaSourceIndex = (_selectedMediaSourceIndex + 1) % _mediaSources.Count;
            NormalizeSelectionStateForCurrentMediaSource(resetSubtitleSelection: true, resetAudioSelection: true);
            HideSelectionPanel();
            UpdateVersionButtonText();
            UpdateMetadataView();
        }

        private void UpdateSubtitleButtonText()
        {
            if (_subtitleButton == null) return;
            var label = _subtitleButton.Children[0] as TextLabel;
            if (label == null) return;

            if (!_selectedSubtitleIndex.HasValue)
            {
                label.Text = "Subtitles: Off";
            }
            else
            {
                var stream = GetAvailableSubtitleStreams().Find(s => s.Index == _selectedSubtitleIndex.Value);
                label.Text = stream == null
                    ? "Subtitles: Off"
                    : $"Subtitles: {NormalizeLanguageLabel(stream.Language)}";
            }
        }

        private void UpdateAudioButtonText()
        {
            if (_audioButton == null)
                return;

            if (!(_audioButton.Children[0] is TextLabel label))
                return;

            var audioStreams = GetAvailableAudioStreams();
            int? effectiveAudioIndex = GetEffectiveSelectedAudioIndex(audioStreams);
            if (!effectiveAudioIndex.HasValue)
            {
                label.Text = "Audio: Default";
                return;
            }

            var stream = audioStreams.Find(s => s.Index == effectiveAudioIndex.Value);
            label.Text = stream == null
                ? "Audio: Default"
                : $"Audio: {FormatAudioButtonLabel(stream)}";
        }

        private void UpdateVersionButtonText()
        {
            if (_versionButton == null || _versionButton.ChildCount == 0)
                return;

            if (!(_versionButton.GetChildAt(0) is TextLabel label))
                return;

            var total = _mediaSources?.Count ?? 0;
            if (total <= 0)
            {
                label.Text = "Source";
                return;
            }

            _selectedMediaSourceIndex = Math.Clamp(_selectedMediaSourceIndex, 0, total - 1);
            var sourceName = GetMediaSourceDisplayName(_mediaSources[_selectedMediaSourceIndex], _selectedMediaSourceIndex + 1);
            label.Text = sourceName;
        }

        private static string GetMediaSourceDisplayName(MediaSourceInfo source, int fallbackIndex)
        {
            if (source == null)
                return $"Source {fallbackIndex}";
            if (!string.IsNullOrWhiteSpace(source.Name))
                return source.Name.Trim();
            return $"Source {fallbackIndex}";
        }

        private string GetSelectedMediaSourceId()
        {
            if (_mediaSources == null || _mediaSources.Count == 0)
                return null;
            if (_selectedMediaSourceIndex < 0 || _selectedMediaSourceIndex >= _mediaSources.Count)
                return null;
            return _mediaSources[_selectedMediaSourceIndex]?.Id;
        }

        private MediaSourceInfo GetSelectedMediaSource()
        {
            if (_mediaSources == null || _mediaSources.Count == 0)
                return null;

            if (_selectedMediaSourceIndex >= 0 && _selectedMediaSourceIndex < _mediaSources.Count)
                return _mediaSources[_selectedMediaSourceIndex];

            return _mediaSources[0];
        }

        private List<MediaStream> GetAvailableSubtitleStreams()
        {
            var selectedSourceSubtitleStreams = GetSelectedMediaSource()?.MediaStreams?
                .Where(s => string.Equals(s.Type, "Subtitle", StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.Index)
                .ToList();

            if (selectedSourceSubtitleStreams != null && selectedSourceSubtitleStreams.Count > 0)
                return selectedSourceSubtitleStreams;

            return _subtitleStreams?
                .OrderBy(s => s.Index)
                .ToList() ?? new List<MediaStream>();
        }

        private string GetSelectedSubtitleCodec()
        {
            if (!_selectedSubtitleIndex.HasValue)
                return null;

            return GetAvailableSubtitleStreams()
                .FirstOrDefault(s => s.Index == _selectedSubtitleIndex.Value)
                ?.Codec;
        }

        private List<MediaStream> GetAvailableAudioStreams()
        {
            return GetSelectedMediaSource()?.MediaStreams?
                .Where(s => string.Equals(s.Type, "Audio", StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.Index)
                .ToList() ?? new List<MediaStream>();
        }

        private void NormalizeSelectionStateForCurrentMediaSource(bool resetSubtitleSelection = false, bool resetAudioSelection = false)
        {
            var subtitleStreams = GetAvailableSubtitleStreams();
            if (resetSubtitleSelection || (_selectedSubtitleIndex.HasValue && !subtitleStreams.Any(s => s.Index == _selectedSubtitleIndex.Value)))
                _selectedSubtitleIndex = null;

            var audioStreams = GetAvailableAudioStreams();
            int? effectiveAudioIndex = GetEffectiveSelectedAudioIndex(audioStreams);
            _selectedAudioIndex = resetAudioSelection
                ? ResolveDefaultAudioStreamIndex(audioStreams)
                : effectiveAudioIndex;

            UpdateSubtitleButtonText();
            UpdateAudioButtonText();
        }

        private int? GetEffectiveSelectedAudioIndex(List<MediaStream> audioStreams = null)
        {
            audioStreams ??= GetAvailableAudioStreams();
            if (audioStreams == null || audioStreams.Count == 0)
                return null;

            if (_selectedAudioIndex.HasValue && audioStreams.Any(s => s.Index == _selectedAudioIndex.Value))
                return _selectedAudioIndex.Value;

            return ResolveDefaultAudioStreamIndex(audioStreams);
        }

        private static int? ResolveDefaultAudioStreamIndex(List<MediaStream> audioStreams)
        {
            if (audioStreams == null || audioStreams.Count == 0)
                return null;

            var defaultStream = audioStreams.FirstOrDefault(s => s.IsDefault);
            return (defaultStream ?? audioStreams[0]).Index;
        }

        private static string FormatSubtitleStreamOption(MediaStream stream)
        {
            if (stream == null)
                return "Unknown";

            string lang = string.IsNullOrWhiteSpace(stream.Language) ? "UNKNOWN" : stream.Language.ToUpperInvariant();
            string title = string.IsNullOrWhiteSpace(stream.DisplayTitle) ? $"Sub {stream.Index}" : stream.DisplayTitle;
            string label = $"{lang} | {title}";
            if (stream.IsExternal)
                label += " (Ext)";
            return label;
        }

        private static string FormatAudioStreamOption(MediaStream stream)
        {
            if (stream == null)
                return "UNKNOWN | AUDIO";

            string lang = string.IsNullOrWhiteSpace(stream.Language) ? "UNKNOWN" : stream.Language.ToUpperInvariant();
            string codec = string.IsNullOrWhiteSpace(stream.Codec) ? "AUDIO" : stream.Codec.ToUpperInvariant();
            return $"{lang} | {codec}";
        }

        private static string FormatAudioButtonLabel(MediaStream stream)
        {
            if (stream == null)
                return "Default";

            string codec = string.IsNullOrWhiteSpace(stream.Codec) ? "Audio" : stream.Codec.ToUpperInvariant();
            string lang = NormalizeLanguageLabel(stream.Language);
            return string.Equals(lang, "Unknown", StringComparison.Ordinal)
                ? codec
                : $"{lang} | {codec}";
        }

        private static string NormalizeLanguageLabel(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
                return "Unknown";

            string trimmed = language.Trim();
            if (trimmed.Length == 1)
                return trimmed.ToUpperInvariant();

            return char.ToUpper(trimmed[0], CultureInfo.InvariantCulture) + trimmed.Substring(1);
        }

        private View CreateMetadataView()
        {
            var container = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 114,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    CellPadding = new Size2D(0, 10)
                }
            };

            _metadataSummaryRow = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 38,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    CellPadding = new Size2D(18, 0)
                }
            };

            _metadataSummaryLabel = new TextLabel
            {
                WidthResizePolicy = ResizePolicyType.UseNaturalSize,
                HeightResizePolicy = ResizePolicyType.UseNaturalSize,
                PointSize = 28,
                TextColor = new Color(0.88f, 0.88f, 0.88f, 1f),
                VerticalAlignment = VerticalAlignment.Center
            };

            var ratingStar = new TextLabel("\u2605")
            {
                WidthResizePolicy = ResizePolicyType.UseNaturalSize,
                HeightResizePolicy = ResizePolicyType.UseNaturalSize,
                PointSize = 32,
                TextColor = new Color(0.95f, 0.78f, 0.29f, 1f),
                VerticalAlignment = VerticalAlignment.Center
            };

            _metadataRatingLabel = new TextLabel
            {
                WidthResizePolicy = ResizePolicyType.UseNaturalSize,
                HeightResizePolicy = ResizePolicyType.UseNaturalSize,
                PointSize = 28,
                TextColor = new Color(0.92f, 0.92f, 0.92f, 1f),
                VerticalAlignment = VerticalAlignment.Center
            };

            _metadataRatingGroup = new View
            {
                WidthResizePolicy = ResizePolicyType.FitToChildren,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    CellPadding = new Size2D(8, 0)
                }
            };

            _metadataRatingGroup.Add(ratingStar);
            _metadataRatingGroup.Add(_metadataRatingLabel);

            _metadataSummaryRow.Add(_metadataSummaryLabel);
            _metadataSummaryRow.Add(_metadataRatingGroup);

            _metadataTagRow = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 56,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    CellPadding = new Size2D(12, 0),
                    HorizontalAlignment = HorizontalAlignment.Begin
                }
            };

            container.Add(_metadataSummaryRow);
            container.Add(_metadataTagRow);
            return container;
        }

        private void UpdateMetadataView()
        {
            if (_metadataContainer == null || _metadataSummaryLabel == null || _metadataTagRow == null)
                return;

            var summaryText = BuildSummaryText(_episode);
            _metadataSummaryLabel.Text = string.IsNullOrWhiteSpace(summaryText) ? " " : summaryText;

            if (_episode.CommunityRating.HasValue && _episode.CommunityRating.Value > 0)
            {
                _metadataRatingLabel.Text = _episode.CommunityRating.Value.ToString("0.0", CultureInfo.InvariantCulture);
                _metadataRatingGroup.Show();
            }
            else
            {
                _metadataRatingGroup.Hide();
            }

            var tags = BuildTechnicalTags(GetSelectedMediaSource());
            RebuildMetadataTags(tags);

            _metadataSummaryRow.Show();
            _metadataTagRow.Show();
            _metadataContainer.Show();
        }

        private void RebuildMetadataTags(List<string> tags)
        {
            DisposeRowChildren(_metadataTagRow);

            if (tags == null || tags.Count == 0)
                return;

            foreach (var tag in tags)
            {
                var chip = CreateMetadataChip(tag);
                _metadataTagRow.Add(chip);
            }
        }

        private static void DisposeRowChildren(View row)
        {
            while (row != null && row.ChildCount > 0)
            {
                var child = row.GetChildAt(0);
                row.Remove(child);
                try { child.Dispose(); } catch { }
            }
        }

        private static View CreateMetadataChip(string text)
        {
            bool isDolbyVisionChip =
                string.Equals(text, DolbyVisionChipToken, StringComparison.Ordinal) ||
                string.Equals(text?.Trim(), "Dolby Vision", StringComparison.OrdinalIgnoreCase);
            bool isDolbyAudioChip = !isDolbyVisionChip &&
                !string.IsNullOrWhiteSpace(text) &&
                text.StartsWith(DolbyAudioChipPrefix, StringComparison.Ordinal);
            string chipLabelText = isDolbyAudioChip
                ? text.Substring(DolbyAudioChipPrefix.Length).Trim()
                : text;

            var chip = new View
            {
                WidthResizePolicy = ResizePolicyType.FitToChildren,
                HeightSpecification = 48,
                BackgroundColor = UiTheme.DetailsChipSurface,
                CornerRadius = 12.0f,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                ClippingMode = ClippingModeType.ClipChildren,
                Padding = new Extents(12, 12, 8, 8),
                Margin = new Extents(0, 0, 2, 2),
                BorderlineWidth = 1.0f,
                BorderlineColor = new Color(1f, 1f, 1f, 0.14f),
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            bool hasIcon = false;
            if (isDolbyVisionChip || isDolbyAudioChip)
            {
                string iconFile = isDolbyVisionChip ? "dolby_vision.svg" : "dolby_audio.svg";
                string iconPath = System.IO.Path.Combine(Tizen.Applications.Application.Current.DirectoryInfo.SharedResource, iconFile);
                if (File.Exists(iconPath))
                {
                    int iconWidth = isDolbyVisionChip ? 124 : 24;
                    int iconHeight = isDolbyVisionChip ? 22 : 20;
                    chip.Add(new ImageView
                    {
                        WidthSpecification = iconWidth,
                        HeightSpecification = iconHeight,
                        ResourceUrl = iconPath,
                        PreMultipliedAlpha = false,
                        FittingMode = FittingModeType.ShrinkToFit,
                        SamplingMode = SamplingModeType.BoxThenLanczos,
                        Margin = isDolbyVisionChip ? new Extents(0, 0, 0, 0) : new Extents(0, 8, 0, 0)
                    });
                    hasIcon = true;
                }
            }

            if (isDolbyVisionChip)
            {
                chip.Padding = new Extents(10, 10, 8, 8);
                if (hasIcon)
                    return chip;
                chipLabelText = "Dolby Vision";
            }

            var label = new TextLabel(chipLabelText)
            {
                WidthResizePolicy = ResizePolicyType.UseNaturalSize,
                HeightResizePolicy = ResizePolicyType.UseNaturalSize,
                PointSize = 20,
                TextColor = new Color(0.98f, 0.98f, 0.98f, 1f),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            label.SetFontStyle(new Tizen.NUI.Text.FontStyle { Weight = FontWeightType.Bold });

            chip.Add(label);
            return chip;
        }

        private static string BuildSummaryText(JellyfinMovie media)
        {
            if (media == null)
                return string.Empty;

            var parts = new List<string>();

            if (media.ProductionYear > 0)
                parts.Add(media.ProductionYear.ToString(CultureInfo.InvariantCulture));

            var runtime = FormatRuntimeForMetadata(media.RunTimeTicks);
            if (!string.IsNullOrWhiteSpace(runtime))
                parts.Add(runtime);

            if (!string.IsNullOrWhiteSpace(media.OfficialRating))
                parts.Add(media.OfficialRating.Trim());

            return string.Join("  ", parts);
        }

        private static string FormatRuntimeForMetadata(long ticks)
        {
            if (ticks <= 0)
                return null;

            var totalMinutes = (int)Math.Round(TimeSpan.FromTicks(ticks).TotalMinutes, MidpointRounding.AwayFromZero);
            if (totalMinutes <= 0)
                return null;

            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;

            if (hours <= 0)
                return $"{totalMinutes}m";
            if (minutes == 0)
                return $"{hours}h";

            return $"{hours}h {minutes}m";
        }

        private static List<string> BuildTechnicalTags(MediaSourceInfo source)
        {
            var tags = new List<string>();
            if (source?.MediaStreams == null || source.MediaStreams.Count == 0)
                return tags;

            MediaStream videoStream = null;
            MediaStream audioStream = null;

            foreach (var stream in source.MediaStreams)
            {
                if (stream == null)
                    continue;

                if (videoStream == null &&
                    string.Equals(stream.Type, "Video", StringComparison.OrdinalIgnoreCase))
                {
                    videoStream = stream;
                }
                else if (audioStream == null &&
                         string.Equals(stream.Type, "Audio", StringComparison.OrdinalIgnoreCase))
                {
                    audioStream = stream;
                }
            }

            AddMetadataTag(tags, GetResolutionTag(videoStream));
            AddMetadataTag(tags, GetVideoCodecTag(videoStream?.Codec));
            AddMetadataTag(tags, GetDolbyVisionChipTag(videoStream) ?? GetHdrTag(videoStream));
            AddMetadataTag(tags, GetAudioCodecTag(audioStream));
            AddMetadataTag(tags, GetDolbyAudioChipTag(audioStream) ?? GetAudioChannelTag(audioStream));

            while (tags.Count > 5)
                tags.RemoveAt(tags.Count - 1);

            return tags;
        }

        private static string GetResolutionTag(MediaStream stream)
        {
            if (stream == null)
                return null;

            var width = stream.Width.GetValueOrDefault();
            var height = stream.Height.GetValueOrDefault();

            if (width >= 3800 || height >= 2000)
                return "4K";
            if (width >= 1900 || height >= 1000)
                return "1080p";
            if (width >= 1200 || height >= 700)
                return "HD";

            var description = GetStreamSearchText(stream);
            if (description.Contains("2160"))
                return "4K";
            if (description.Contains("1080"))
                return "1080p";
            if (description.Contains("720"))
                return "HD";

            return null;
        }

        private static string GetVideoCodecTag(string codec)
        {
            if (string.IsNullOrWhiteSpace(codec))
                return null;

            var normalized = codec.Trim().ToLowerInvariant();

            if (normalized.Contains("hevc") || normalized.Contains("h265") || normalized.Contains("x265"))
                return "HEVC";
            if (normalized.Contains("h264") || normalized.Contains("avc") || normalized.Contains("x264"))
                return "H.264";
            if (normalized.Contains("av1"))
                return "AV1";
            if (normalized.Contains("vp9"))
                return "VP9";

            return codec.Trim().ToUpperInvariant();
        }

        private static string GetHdrTag(MediaStream stream)
        {
            var text = GetStreamSearchText(stream);
            if (string.IsNullOrWhiteSpace(text))
                return null;

            if (text.Contains("hdr10+"))
                return "HDR10+";
            if (text.Contains("hdr10"))
                return "HDR10";
            if (text.Contains("hlg"))
                return "HLG";
            if (text.Contains("hdr"))
                return "HDR";

            return null;
        }

        private static string GetAudioCodecTag(MediaStream stream)
        {
            if (stream == null)
                return null;

            var text = GetStreamSearchText(stream);
            var codec = stream.Codec?.ToLowerInvariant() ?? string.Empty;

            if (text.Contains("dolby digital plus") || text.Contains("eac3") || codec.Contains("eac3"))
                return "Dolby Digital+";
            if (text.Contains("dolby digital") || codec == "ac3" || codec.Contains("ac3"))
                return "Dolby Digital";
            if (text.Contains("truehd") || codec.Contains("truehd"))
                return "TrueHD";
            if (text.Contains("dts") || codec.Contains("dts"))
                return "DTS";
            if (text.Contains("aac") || codec.Contains("aac"))
                return "AAC";
            if (text.Contains("flac") || codec.Contains("flac"))
                return "FLAC";
            if (text.Contains("opus") || codec.Contains("opus"))
                return "Opus";
            if (text.Contains("mp3") || codec.Contains("mp3"))
                return "MP3";

            if (!string.IsNullOrWhiteSpace(stream.Codec))
                return stream.Codec.Trim().ToUpperInvariant();

            return null;
        }

        private static string GetDolbyVisionChipTag(MediaStream stream)
        {
            var text = GetStreamSearchText(stream);
            if (string.IsNullOrWhiteSpace(text))
                return null;

            if (text.Contains("dolby vision") || text.Contains("dovi") || text.Contains("dvhe"))
                return DolbyVisionChipToken;

            return null;
        }

        private static string GetDolbyAudioChipTag(MediaStream stream)
        {
            if (stream == null)
                return null;

            var text = GetStreamSearchText(stream);
            var codec = stream.Codec?.ToLowerInvariant() ?? string.Empty;
            bool isDolbyAudio = text.Contains("dolby") ||
                                codec.Contains("eac3") ||
                                codec.Contains("ac3") ||
                                codec.Contains("truehd");
            if (!isDolbyAudio)
                return null;

            if (text.Contains("atmos"))
                return $"{DolbyAudioChipPrefix}Atmos";

            var channels = GetAudioChannelTag(stream);
            if (channels == "5.1" || channels == "7.1")
                return $"{DolbyAudioChipPrefix}{channels}";

            return null;
        }

        private static string GetAudioChannelTag(MediaStream stream)
        {
            if (stream == null)
                return null;

            if (!string.IsNullOrWhiteSpace(stream.ChannelLayout))
            {
                var layout = stream.ChannelLayout.ToLowerInvariant();

                if (layout.Contains("7.1"))
                    return "7.1";
                if (layout.Contains("6.1"))
                    return "6.1";
                if (layout.Contains("5.1"))
                    return "5.1";
                if (layout.Contains("2.0") || layout.Contains("stereo"))
                    return "2.0";
                if (layout.Contains("1.0") || layout.Contains("mono"))
                    return "1.0";
            }

            if (stream.Channels.HasValue && stream.Channels.Value > 0)
            {
                return stream.Channels.Value switch
                {
                    8 => "7.1",
                    7 => "6.1",
                    6 => "5.1",
                    2 => "2.0",
                    1 => "1.0",
                    _ => $"{stream.Channels.Value}.0"
                };
            }

            var text = GetStreamSearchText(stream);
            if (text.Contains("7.1"))
                return "7.1";
            if (text.Contains("6.1"))
                return "6.1";
            if (text.Contains("5.1"))
                return "5.1";
            if (text.Contains("2.0") || text.Contains("stereo"))
                return "2.0";

            return null;
        }

        private static string GetStreamSearchText(MediaStream stream)
        {
            if (stream == null)
                return string.Empty;

            return $"{stream.DisplayTitle} {stream.VideoRange} {stream.ChannelLayout} {stream.Codec}".ToLowerInvariant();
        }

        private static void AddMetadataTag(List<string> tags, string value)
        {
            if (tags == null || string.IsNullOrWhiteSpace(value))
                return;

            var normalized = value.Trim();
            foreach (var existing in tags)
            {
                if (string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            tags.Add(normalized);
        }

        private void PlayMedia(JellyfinMovie media, int startPositionMs)
        {
            NavigationService.Navigate(
                new VideoPlayerScreen(
                    media,
                    startPositionMs,
                    _selectedSubtitleIndex,
                    AppState.BurnInSubtitles,
                    GetSelectedMediaSourceId(),
                    GetEffectiveSelectedAudioIndex(),
                    GetSelectedSubtitleCodec()
                )
            );
        }
        private static int TicksToMs(long ticks)
        {
            if (ticks <= 0)
                return 0;
            var ms = ticks / 10000;
            return (int)Math.Clamp(ms, 0, int.MaxValue);
        }
    }
}
