using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using Tizen.NUI.Components;
using JellyfinTizen.Core;
using JellyfinTizen.Models;

namespace JellyfinTizen.Screens
{
    public class EpisodeDetailsScreen : ScreenBase, IKeyHandler
    {
        private const int PosterWidth = 420;
        private const int PosterHeight = 630;
        private readonly JellyfinMovie _episode;
        private readonly bool _resumeAvailable;
        private View _playButton;
        private View _resumeButton;
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

        public EpisodeDetailsScreen(JellyfinMovie episode)
        {
            _episode = episode;
            _resumeAvailable = episode.PlaybackPositionTicks > 0;
            var root = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent
            };
            var apiKey = Uri.EscapeDataString(AppState.AccessToken);
            var serverUrl = AppState.Jellyfin.ServerUrl;
            var backdropUrl =
                $"{serverUrl}/Items/{_episode.SeriesId}/Images/Backdrop/0" +
                "?maxWidth=1920&quality=90" +
                $"&api_key={apiKey}";
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
                BackgroundColor = new Color(0, 0, 0, 0.65f)
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
            
            // Episode images are usually rectangular thumbnails, not posters
            const int EpisodeThumbWidth = 640;
            const int EpisodeThumbHeight = 360;
            
            var thumbUrl = _episode.HasThumb
                ? $"{serverUrl}/Items/{_episode.Id}/Images/Thumb/0?maxWidth={EpisodeThumbWidth}&quality=95&api_key={apiKey}"
                : $"{serverUrl}/Items/{_episode.Id}/Images/Primary/0?maxWidth={EpisodeThumbWidth}&quality=95&api_key={apiKey}";
            
            var thumbFrame = new View
            {
                WidthSpecification = EpisodeThumbWidth,
                HeightSpecification = EpisodeThumbHeight,
                BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f),
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
                HeightResizePolicy = ResizePolicyType.FillToParent,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    CellPadding = new Size2D(0, 26)
                }
            };
            var titleText = $"{_episode.SeriesName} - {_episode.Name}";
            var title = new TextLabel(titleText)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                PointSize = 64,
                TextColor = Color.White,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word
            };
            var overviewViewport = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 360,
                ClippingMode = ClippingModeType.ClipChildren
            };

            var overview = new TextLabel(
                string.IsNullOrEmpty(_episode.Overview)
                    ? "No overview available."
                    : _episode.Overview
            )
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.UseNaturalSize,
                PointSize = 32,
                TextColor = new Color(0.85f, 0.85f, 0.85f, 1f),
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word,
                Ellipsis = false
            };

            overviewViewport.Add(overview);
            _infoColumn.Add(title);
            _infoColumn.Add(overviewViewport);
            _buttonGroup = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    CellPadding = new Size2D(0, 14)
                },
                Margin = new Extents(0, 0, 20, 0)
            };

            _buttonRowTop = CreateButtonRow();
            _buttonRowBottom = CreateButtonRow();
            _buttonGroup.Add(_buttonRowTop);

            _playButton = CreateActionButton("Play", isPrimary: true);
            _playButton.WidthSpecification = 200;

            if (_resumeAvailable)
            {
                var resumeText = $"Resume ({FormatResumeTime(_episode.PlaybackPositionTicks)})";
                _resumeButton = CreateActionButton(resumeText, isPrimary: false);
            }

            _subtitleButton = CreateActionButton("Subtitles: Off", isPrimary: false);

            _versionButton = CreateActionButton("Default", isPrimary: false);
            RebuildActionButtons(includeVersionButton: false);

            _infoColumn.Add(_buttonGroup);
            content.Add(thumbFrame);
            content.Add(_infoColumn);
            root.Add(backdrop);
            root.Add(dimOverlay);
            root.Add(content);
            Add(root);
        }
        public override void OnShow()
        {
            _ = LoadSubtitleStreamsAsync();
            _ = LoadMediaSourcesAsync();
            FocusButton(0);
        }

        private async Task LoadSubtitleStreamsAsync()
        {
            try
            {
                _subtitleStreams = await AppState.Jellyfin.GetSubtitleStreamsAsync(_episode.Id);
                UpdateSubtitleButtonText();
            }
            catch
            {
                // Ignore
            }
        }

        private async Task LoadMediaSourcesAsync()
        {
            try
            {
                var playbackInfo = await AppState.Jellyfin.GetPlaybackInfoAsync(_episode.Id);
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

            _selectedMediaSourceIndex = Math.Clamp(_selectedMediaSourceIndex, 0, _mediaSources.Count - 1);
            RebuildActionButtons(includeVersionButton: _mediaSources.Count > 1);
            UpdateVersionButtonText();

            if (_buttons.Count > 0)
                FocusButton(_buttonIndex);
        }

        public void HandleKey(AppKey key)
        {
            switch (key)
            {
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
        private View CreateActionButton(string text, bool isPrimary)
        {
            var button = new View
            {
                WidthSpecification = 260,
                HeightSpecification = 70,
                BackgroundColor = new Color(1, 1, 1, 0.15f),
                Focusable = true,
                CornerRadius = 35.0f
            };
            var label = new TextLabel(text)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                PointSize = 26,
                TextColor = Color.White,
                Ellipsis = true
            };
            button.Add(label);
            return button;
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

            AddActionButton(_playButton);
            if (_resumeButton != null)
                AddActionButton(_resumeButton);
            AddActionButton(_subtitleButton);
            if (includeVersionButton)
                AddActionButton(_versionButton);

            if (_buttonRowBottom.ChildCount > 0)
            {
                if (!HasChild(_buttonGroup, _buttonRowBottom))
                    _buttonGroup.Add(_buttonRowBottom);
            }
            else if (HasChild(_buttonGroup, _buttonRowBottom))
            {
                _buttonGroup.Remove(_buttonRowBottom);
            }

            _buttonIndex = Math.Clamp(_buttonIndex, 0, Math.Max(0, _buttons.Count - 1));

            void AddActionButton(View button)
            {
                if (button == null)
                    return;

                var count = _buttons.Count;
                _buttons.Add(button);

                if (count < 3)
                    _buttonRowTop.Add(button);
                else
                    _buttonRowBottom.Add(button);
            }
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
                button.Scale = focused ? new Vector3(1.08f, 1.08f, 1f) : Vector3.One;

                if (focused)
                {
                    button.BackgroundColor = new Color(0.85f, 0.11f, 0.11f, 1f); 
                }
                else
                {
                    button.BackgroundColor = new Color(1, 1, 1, 0.15f);
                }
            }

            if (_buttonIndex >= 0)
            {
                FocusManager.Instance.SetCurrentFocusView(_buttons[_buttonIndex]);
            }
        }
        private void ActivateFocusedButton()
        {
            if (_buttonIndex == 0)
            {
                PlayMedia(_episode, 0);
                return;
            }
            if (_resumeAvailable && _buttonIndex == 1)
            {
                PlayMedia(_episode, TicksToMs(_episode.PlaybackPositionTicks));
                return;
            }
            if (_buttons[_buttonIndex] == _subtitleButton)
            {
                CycleSubtitle();
                return;
            }
            if (_buttons[_buttonIndex] == _versionButton)
            {
                CycleMediaSource();
                return;
            }
        }

        private void CycleSubtitle()
        {
            if (_subtitleStreams == null || _subtitleStreams.Count == 0) return;

            if (_selectedSubtitleIndex == null)
            {
                _selectedSubtitleIndex = _subtitleStreams[0].Index;
            }
            else
            {
                int currentListIndex = _subtitleStreams.FindIndex(s => s.Index == _selectedSubtitleIndex);
                if (currentListIndex == -1 || currentListIndex == _subtitleStreams.Count - 1)
                {
                    _selectedSubtitleIndex = null;
                }
                else
                {
                    _selectedSubtitleIndex = _subtitleStreams[currentListIndex + 1].Index;
                }
            }
            UpdateSubtitleButtonText();
        }

        private void CycleMediaSource()
        {
            if (_mediaSources == null || _mediaSources.Count <= 1)
            {
                UpdateVersionButtonText();
                return;
            }

            _selectedMediaSourceIndex = (_selectedMediaSourceIndex + 1) % _mediaSources.Count;
            _selectedSubtitleIndex = null;
            UpdateSubtitleButtonText();
            UpdateVersionButtonText();
        }

        private void UpdateSubtitleButtonText()
        {
            if (_subtitleButton == null) return;
            var label = _subtitleButton.Children[0] as TextLabel;
            if (label == null) return;

            if (_selectedSubtitleIndex == null)
            {
                label.Text = "Subtitles: Off";
            }
            else
            {
                var stream = _subtitleStreams?.Find(s => s.Index == _selectedSubtitleIndex);
                string lang = stream?.Language ?? "Unknown";
                if (!string.IsNullOrEmpty(lang) && lang.Length > 1) 
                    lang = char.ToUpper(lang[0]) + lang.Substring(1);
                label.Text = $"Subtitles: {lang}";
            }
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

        private void PlayMedia(JellyfinMovie media, int startPositionMs)
        {
            NavigationService.Navigate(
                new VideoPlayerScreen(
                    media,
                    startPositionMs,
                    _selectedSubtitleIndex,
                    AppState.BurnInSubtitles,
                    GetSelectedMediaSourceId()
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
        private static string FormatResumeTime(long ticks)
        {
            var ms = TicksToMs(ticks);
            if (ms <= 0)
                return "00:00";
            var t = TimeSpan.FromMilliseconds(ms);
            return t.Hours > 0
                ? $"{t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}"
                : $"{t.Minutes:D2}:{t.Seconds:D2}";
        }
    }
}
