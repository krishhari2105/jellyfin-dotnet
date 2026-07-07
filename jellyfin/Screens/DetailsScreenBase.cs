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
using IOPath = System.IO.Path;

namespace JellyfinTizen.Screens
{
    public abstract class DetailsScreenBase : ScreenBase, IKeyHandler
    {
        // =====================================================================
        // SHARED ENUMS
        // =====================================================================
        protected enum DetailsPanelMode
        {
            None,
            Subtitle,
            Audio
        }

        // =====================================================================
        // ABSTRACT / VIRTUAL MEMBERS (must be implemented by subclasses)
        // =====================================================================
        protected abstract JellyfinMovie GetMediaItem();
        protected abstract int ThumbnailWidthForLayout { get; }
        protected virtual bool UseFallbackForResolution => false;

        // =====================================================================
        // CONSTANTS (identical in both original screens)
        // =====================================================================
        protected const float ButtonFocusScale = 1.08f;
        protected const int ActionButtonHeight = 70;
        protected const int ActionButtonRowGap = 28;
        protected const int SecondaryActionButtonWidth = 240;
        protected const int PlayActionButtonWidth = 176;
        protected const int IconActionButtonWidth = 122;
        protected const int ActionButtonIconLabelGap = 6;
        protected const int PlayActionButtonIconSize = 46;
        protected const int AudioActionButtonIconSize = 36;
        protected const int SubtitleActionButtonIconSize = 34;
        protected const int DetailsHorizontalPadding = 90;
        protected const int DetailsColumnGap = 60;
        protected const string DolbyAudioChipPrefix = "__DOLBY_AUDIO__:";
        protected const string DolbyVisionChipToken = "__DOLBY_VISION_ICON__";

        // =====================================================================
        // SHARED INSTANCE FIELDS
        // =====================================================================
        protected bool _resumeAvailable;
        protected View _playButton;
        protected View _resumeButton;
        protected View _audioButton;
        protected View _subtitleButton;
        protected View _versionButton;
        protected readonly List<View> _buttons = new();
        protected int _buttonIndex;
        protected View _infoColumn;
        protected View _buttonGroup;
        protected View _buttonRowTop;
        protected View _buttonRowBottom;
        protected List<MediaStream> _subtitleStreams;
        protected List<MediaSourceInfo> _mediaSources = new();
        protected int _selectedMediaSourceIndex = 0;
        protected int? _selectedSubtitleIndex = null;
        protected int? _selectedAudioIndex = null;
        protected bool _subtitleStreamsLoaded;
        protected bool _mediaSourcesLoaded;
        protected readonly Dictionary<View, Animation> _focusAnimations = new();
        protected readonly Dictionary<string, string> _darkButtonIconPathCache = new(StringComparer.OrdinalIgnoreCase);
        protected DetailsSelectionPanel _selectionPanel;
        protected DetailsPanelMode _selectionPanelMode;
        protected bool _actionButtonReflowScheduled;

        // =====================================================================
        // CONSTRUCTOR
        // =====================================================================
        protected DetailsScreenBase(JellyfinMovie mediaItem)
        {
            _resumeAvailable = mediaItem.PlaybackPositionTicks > 0;
            _selectionPanel = new DetailsSelectionPanel(this);
            // NormalizeSelectionStateForCurrentMediaSource() is called by derived constructors after their field init
        }

        // =====================================================================
        // LAYOUT HELPERS (identical implementations)
        // =====================================================================
        protected static View CreateButtonRow()
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

        protected int ResolveTopActionRowAvailableWidth()
        {
            int groupWidth = (int)Math.Round(_buttonGroup?.SizeWidth ?? 0);
            int infoWidth = (int)Math.Round(_infoColumn?.SizeWidth ?? 0);
            int fallbackWidth = Math.Max(320, Window.Default.Size.Width - (DetailsHorizontalPadding * 2) - ThumbnailWidthForLayout - DetailsColumnGap);
            return Math.Max(fallbackWidth, Math.Max(infoWidth, groupWidth));
        }

        // =====================================================================
        // ACTION BUTTON CREATION (identical)
        // =====================================================================
        protected View CreateActionButton(string text, bool isPrimary, string iconFile = null, int? width = SecondaryActionButtonWidth, int iconSize = 30)
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

        protected void ApplyActionButtonVisual(View button, bool focused)
        {
            if (button == null) return;
            UiFactory.SetButtonFocusState(button, focused: focused);
            ApplyActionButtonIconState(button, focused);
        }

        protected void ApplyActionButtonIconState(View view, bool focused)
        {
            if (view == null) return;
            if (view is ImageView icon && !string.IsNullOrWhiteSpace(icon.Name))
                icon.ResourceUrl = ResolveActionIconPath(icon.Name, focused);
            uint childCount = view.ChildCount;
            for (uint i = 0; i < childCount; i++)
            {
                if (view.GetChildAt(i) is View child)
                    ApplyActionButtonIconState(child, focused);
            }
        }

        protected string ResolveActionIconPath(string iconFile, bool focused)
        {
            if (string.IsNullOrWhiteSpace(iconFile)) return string.Empty;

            string sourcePath = IOPath.Combine(Tizen.Applications.Application.Current.DirectoryInfo.SharedResource, iconFile);
            if (!focused || !iconFile.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) || !File.Exists(sourcePath))
                return sourcePath;

            string versionToken;
            try
            {
                versionToken = File.GetLastWriteTimeUtc(sourcePath).Ticks.ToString(CultureInfo.InvariantCulture);
            }
            catch { versionToken = "0"; }

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

                string cacheDir = IOPath.Combine(Tizen.Applications.Application.Current.DirectoryInfo.Data, "icon-cache");
                Directory.CreateDirectory(cacheDir);

                string baseName = IOPath.GetFileNameWithoutExtension(iconFile);
                string darkPath = IOPath.Combine(cacheDir, $"{baseName}_dark_{versionToken}.svg");
                if (!File.Exists(darkPath))
                    File.WriteAllText(darkPath, darkSvg);

                _darkButtonIconPathCache[cacheKey] = darkPath;
                return darkPath;
            }
            catch { return sourcePath; }
        }

        // =====================================================================
        // BUTTON LAYOUT / FOCUS (identical)
        // =====================================================================
        protected void RebuildActionButtons(bool includeVersionButton)
        {
            if (_buttonGroup == null || _buttonRowTop == null || _buttonRowBottom == null)
                return;

            DetailsScreenHelpers.ClearRowChildren(_buttonRowTop);
            DetailsScreenHelpers.ClearRowChildren(_buttonRowBottom);
            _buttons.Clear();
            int topRowAvailableWidth = ResolveTopActionRowAvailableWidth();
            int topRowUsedWidth = 0;
            bool wrappedToSecondRow = false;

            AddActionButton(_playButton);
            if (_resumeButton != null) AddActionButton(_resumeButton);
            AddActionButton(_audioButton);
            AddActionButton(_subtitleButton);
            if (includeVersionButton) AddActionButton(_versionButton);

            _buttonIndex = Math.Clamp(_buttonIndex, 0, Math.Max(0, _buttons.Count - 1));

            void AddActionButton(View button)
            {
                if (button == null) return;
                _buttons.Add(button);
                if (wrappedToSecondRow)
                {
                    _buttonRowBottom.Add(button);
                    return;
                }

                int buttonWidth = DetailsScreenHelpers.EstimateActionButtonWidth(button);
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

        protected void MoveFocus(int delta)
        {
            if (_buttons.Count == 0) return;
            var newIndex = Math.Clamp(_buttonIndex + delta, 0, _buttons.Count - 1);
            FocusButton(newIndex);
        }

        protected void FocusButton(int index)
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
                FocusManager.Instance.SetCurrentFocusView(_buttons[_buttonIndex]);
        }

        protected void AnimateScale(View view, Vector3 targetScale)
        {
            if (view == null) return;
            if (_focusAnimations.TryGetValue(view, out var existing))
            {
                UiAnimator.StopAndDispose(ref existing);
                _focusAnimations.Remove(view);
            }
            var animation = UiAnimator.Start(
                UiAnimator.FocusDurationMs,
                anim => anim.AnimateTo(view, "Scale", targetScale),
                () => _focusAnimations.Remove(view));
            _focusAnimations[view] = animation;
        }

        protected void SetScaleInstant(View view, Vector3 targetScale)
        {
            if (view == null) return;
            if (_focusAnimations.TryGetValue(view, out var existing))
            {
                UiAnimator.StopAndDispose(ref existing);
                _focusAnimations.Remove(view);
            }
            view.Scale = targetScale;
        }

        // =====================================================================
        // KEY HANDLING (shared base — Enter/Back handled here, Left/Right/Up/Down vary)
        // =====================================================================
        public virtual void HandleKey(AppKey key)
        {
            if (HandleSelectionPanelKey(key)) return;

            switch (key)
            {
                case AppKey.Enter:
                    ActivateFocusedButton();
                    break;
                case AppKey.Back:
                    NavigationService.NavigateBack();
                    break;
                // Left/Right/Up/Down handled by subclasses
            }
        }

        protected bool HandleSelectionPanelKey(AppKey key)
        {
            if (_selectionPanel == null || !_selectionPanel.IsVisible) return false;
            switch (key)
            {
                case AppKey.Up: _selectionPanel.MoveSelection(-1); break;
                case AppKey.Down: _selectionPanel.MoveSelection(1); break;
                case AppKey.Enter: ApplyPanelSelection(); break;
                case AppKey.Back: HideSelectionPanel(); break;
            }
            return true;
        }

        protected void ActivateFocusedButton()
        {
            if (_buttonIndex < 0 || _buttonIndex >= _buttons.Count) return;
            var focusedButton = _buttons[_buttonIndex];
            if (focusedButton == _playButton)
            {
                PlayMedia(GetMediaItem(), 0);
                return;
            }
            if (_resumeButton != null && focusedButton == _resumeButton)
            {
                PlayMedia(GetMediaItem(), TicksToMs(GetMediaItem().PlaybackPositionTicks));
                return;
            }
            if (focusedButton == _audioButton) { ShowAudioPanel(); return; }
            if (focusedButton == _subtitleButton) { ShowSubtitlePanel(); return; }
            if (focusedButton == _versionButton) { CycleMediaSource(); return; }
        }

        // =====================================================================
        // SELECTION PANELS (identical)
        // =====================================================================
        protected void ShowSubtitlePanel()
        {
            if (_selectionPanel == null) return;
            var subtitleStreams = GetAvailableSubtitleStreams();
            var options = new List<DetailsSelectionOption> { new DetailsSelectionOption("OFF_INDEX", "OFF") };
            int selectedIndex = 0;
            foreach (var stream in subtitleStreams)
            {
                options.Add(new DetailsSelectionOption(stream.Index.ToString(CultureInfo.InvariantCulture), DetailsScreenHelpers.FormatSubtitleStreamOption(stream)));
                if (_selectedSubtitleIndex.HasValue && stream.Index == _selectedSubtitleIndex.Value)
                    selectedIndex = options.Count - 1;
            }
            _selectionPanelMode = DetailsPanelMode.Subtitle;
            _selectionPanel.Show("Subtitles", options, selectedIndex, UiTheme.PlayerOverlayItem);
        }

        protected void ShowAudioPanel()
        {
            if (_selectionPanel == null) return;
            var audioStreams = GetAvailableAudioStreams();
            if (audioStreams.Count == 0) return;
            int? selectedAudioIndex = GetEffectiveSelectedAudioIndex(audioStreams);
            int selectedIndex = 0;
            var options = new List<DetailsSelectionOption>(audioStreams.Count);
            foreach (var stream in audioStreams)
            {
                options.Add(new DetailsSelectionOption(stream.Index.ToString(CultureInfo.InvariantCulture), DetailsScreenHelpers.FormatAudioStreamOption(stream)));
                if (selectedAudioIndex.HasValue && stream.Index == selectedAudioIndex.Value)
                    selectedIndex = options.Count - 1;
            }
            _selectionPanelMode = DetailsPanelMode.Audio;
            _selectionPanel.Show("Audio Tracks", options, selectedIndex, UiTheme.PlayerAudioItem);
        }

        protected void ApplyPanelSelection()
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
                            ? subtitleIndex : null;
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

        protected void HideSelectionPanel()
        {
            _selectionPanelMode = DetailsPanelMode.None;
            _selectionPanel?.Hide();
        }

        // =====================================================================
        // MEDIA SOURCE / STREAM MANAGEMENT (identical)
        // =====================================================================
        protected void CycleMediaSource()
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
        }

        protected void UpdateSubtitleButtonText()
        {
            if (_subtitleButton == null) return;
            var label = _subtitleButton.Children[0] as TextLabel;
            if (label == null) return;
            if (!_selectedSubtitleIndex.HasValue) label.Text = "Subtitles: Off";
            else
            {
                var stream = GetAvailableSubtitleStreams().Find(s => s.Index == _selectedSubtitleIndex.Value);
                label.Text = stream == null ? "Subtitles: Off" : $"Subtitles: {DetailsScreenHelpers.NormalizeLanguageLabel(stream.Language)}";
            }
        }

        protected void UpdateAudioButtonText()
        {
            if (_audioButton == null) return;
            if (!(_audioButton.Children[0] is TextLabel label)) return;
            var audioStreams = GetAvailableAudioStreams();
            int? effectiveAudioIndex = GetEffectiveSelectedAudioIndex(audioStreams);
            if (!effectiveAudioIndex.HasValue) { label.Text = "Audio: Default"; return; }
            var stream = audioStreams.Find(s => s.Index == effectiveAudioIndex.Value);
            label.Text = stream == null ? "Audio: Default" : $"Audio: {DetailsScreenHelpers.FormatAudioButtonLabel(stream)}";
        }

        protected void UpdateVersionButtonText()
        {
            if (_versionButton == null || _versionButton.ChildCount == 0) return;
            if (!(_versionButton.GetChildAt(0) is TextLabel label)) return;
            var total = _mediaSources?.Count ?? 0;
            if (total <= 0) { label.Text = "Source"; return; }
            _selectedMediaSourceIndex = Math.Clamp(_selectedMediaSourceIndex, 0, total - 1);
            var sourceName = GetMediaSourceDisplayName(_mediaSources[_selectedMediaSourceIndex], _selectedMediaSourceIndex + 1);
            label.Text = sourceName;
        }

        protected static string GetMediaSourceDisplayName(MediaSourceInfo source, int fallbackIndex)
        {
            if (source == null) return $"Source {fallbackIndex}";
            if (!string.IsNullOrWhiteSpace(source.Name)) return source.Name.Trim();
            return $"Source {fallbackIndex}";
        }

        protected string GetSelectedMediaSourceId()
        {
            if (_mediaSources == null || _mediaSources.Count == 0) return null;
            if (_selectedMediaSourceIndex < 0 || _selectedMediaSourceIndex >= _mediaSources.Count) return null;
            return _mediaSources[_selectedMediaSourceIndex]?.Id;
        }

        protected MediaSourceInfo GetSelectedMediaSource()
        {
            if (_mediaSources == null || _mediaSources.Count == 0) return null;
            if (_selectedMediaSourceIndex >= 0 && _selectedMediaSourceIndex < _mediaSources.Count)
                return _mediaSources[_selectedMediaSourceIndex];
            return _mediaSources[0];
        }

        protected List<MediaStream> GetAvailableSubtitleStreams()
        {
            var selectedSourceSubtitleStreams = GetSelectedMediaSource()?.MediaStreams?
                .Where(s => string.Equals(s.Type, "Subtitle", StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.Index)
                .ToList();
            if (selectedSourceSubtitleStreams != null && selectedSourceSubtitleStreams.Count > 0)
                return selectedSourceSubtitleStreams;
            return _subtitleStreams?.OrderBy(s => s.Index).ToList() ?? new List<MediaStream>();
        }

        protected string GetSelectedSubtitleCodec()
        {
            if (!_selectedSubtitleIndex.HasValue) return null;
            return GetAvailableSubtitleStreams().FirstOrDefault(s => s.Index == _selectedSubtitleIndex.Value)?.Codec;
        }

        protected List<MediaStream> GetAvailableAudioStreams()
        {
            return GetSelectedMediaSource()?.MediaStreams?
                .Where(s => string.Equals(s.Type, "Audio", StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.Index)
                .ToList() ?? new List<MediaStream>();
        }

        protected void NormalizeSelectionStateForCurrentMediaSource(bool resetSubtitleSelection = false, bool resetAudioSelection = false)
        {
            var subtitleStreams = GetAvailableSubtitleStreams();
            if (resetSubtitleSelection || (_selectedSubtitleIndex.HasValue && !subtitleStreams.Any(s => s.Index == _selectedSubtitleIndex.Value)))
                _selectedSubtitleIndex = null;

            var audioStreams = GetAvailableAudioStreams();
            int? effectiveAudioIndex = GetEffectiveSelectedAudioIndex(audioStreams);
            _selectedAudioIndex = resetAudioSelection ? ResolveDefaultAudioStreamIndex(audioStreams) : effectiveAudioIndex;

            UpdateSubtitleButtonText();
            UpdateAudioButtonText();
        }

        protected int? GetEffectiveSelectedAudioIndex(List<MediaStream> audioStreams = null)
        {
            audioStreams ??= GetAvailableAudioStreams();
            if (audioStreams == null || audioStreams.Count == 0) return null;
            if (_selectedAudioIndex.HasValue && audioStreams.Any(s => s.Index == _selectedAudioIndex.Value))
                return _selectedAudioIndex.Value;
            return ResolveDefaultAudioStreamIndex(audioStreams);
        }

        protected static int? ResolveDefaultAudioStreamIndex(List<MediaStream> audioStreams)
        {
            if (audioStreams == null || audioStreams.Count == 0) return null;
            var defaultStream = audioStreams.FirstOrDefault(s => s.IsDefault);
            return (defaultStream ?? audioStreams[0]).Index;
        }

        // =====================================================================
        // PLAYBACK (abstract — subclass provides media item)
        // =====================================================================
        protected abstract void PlayMedia(JellyfinMovie media, int startPositionMs);

        protected static int TicksToMs(long ticks)
        {
            if (ticks <= 0) return 0;
            var ms = ticks / 10000;
            return (int)Math.Clamp(ms, 0, int.MaxValue);
        }

        // =====================================================================
        // STATIC HELPERS (identical)
        // =====================================================================
        protected static int EstimateActionButtonWidth(View button)
        {
            if (button == null) return 0;
            int actualWidth = (int)Math.Round(button.SizeWidth);
            if (actualWidth > 0) return actualWidth;
            int specifiedWidth = (int)Math.Round((double)(float)button.WidthSpecification);
            if (specifiedWidth > 0) return specifiedWidth;
            string buttonText = FindActionButtonText(button);
            bool hasIcon = ContainsActionButtonIcon(button);
            int iconWidth = hasIcon ? 46 : 0;
            int textWidth = EstimateActionButtonTextWidth(buttonText);
            int contentGap = hasIcon && !string.IsNullOrWhiteSpace(buttonText) ? 14 : 0;
            int paddingWidth = 68;
            int estimatedWidth = paddingWidth + iconWidth + contentGap + textWidth;
            return Math.Clamp(estimatedWidth, 180, 620);
        }

        protected static int EstimateActionButtonTextWidth(string text)
        {
            int length = string.IsNullOrWhiteSpace(text) ? 0 : text.Trim().Length;
            if (length <= 0) return 0;
            return Math.Clamp(40 + (length * 15), 80, 360);
        }

        protected static string FindActionButtonText(View view)
        {
            if (view == null) return null;
            if (view is TextLabel label && !string.IsNullOrWhiteSpace(label.Text)) return label.Text;
            uint childCount = view.ChildCount;
            for (uint i = 0; i < childCount; i++)
            {
                if (view.GetChildAt(i) is View child)
                {
                    string childText = FindActionButtonText(child);
                    if (!string.IsNullOrWhiteSpace(childText)) return childText;
                }
            }
            return null;
        }

        protected static bool ContainsActionButtonIcon(View view)
        {
            if (view == null) return false;
            if (view is ImageView) return true;
            uint childCount = view.ChildCount;
            for (uint i = 0; i < childCount; i++)
            {
                if (view.GetChildAt(i) is View child && ContainsActionButtonIcon(child))
                    return true;
            }
            return false;
        }

        protected static void ClearRowChildren(View row)
        {
            while (row != null && row.ChildCount > 0)
            {
                var child = row.GetChildAt(0);
                row.Remove(child);
            }
        }

        protected static bool HasChild(View parent, View child)
        {
            if (parent == null || child == null) return false;
            foreach (var existing in parent.Children)
                if (ReferenceEquals(existing, child)) return true;
            return false;
        }
    }
}