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
    public class MovieDetailsScreen : ScreenBase, IKeyHandler
    {
        private const int PosterWidth = 420;
        private const int PosterHeight = 630;
        private readonly JellyfinMovie _mediaItem;
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
        private View _episodesView;
        private List<JellyfinMovie> _episodes;
        private readonly List<View> _episodeViews = new();
        private int _episodeIndex = -1;
        private bool _isEpisodeViewFocused;

        private List<MediaStream> _subtitleStreams;
        private List<MediaSourceInfo> _mediaSources = new();
        private int _selectedMediaSourceIndex = 0;
        private int? _selectedSubtitleIndex = null;

        public MovieDetailsScreen(JellyfinMovie movie)
        {
            _mediaItem = movie;
            _resumeAvailable = movie.PlaybackPositionTicks > 0;
            var root = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent
            };
            var apiKey = Uri.EscapeDataString(AppState.AccessToken);
            var serverUrl = AppState.Jellyfin.ServerUrl;
            var backdropUrl =
                $"{serverUrl}/Items/{(_mediaItem.ItemType == "Episode" ? _mediaItem.SeriesId : _mediaItem.Id)}/Images/Backdrop/0" +
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
            var posterUrl =
                $"{serverUrl}/Items/{_mediaItem.Id}/Images/Primary/0" +
                $"?maxWidth={PosterWidth}&quality=95&api_key={apiKey}";
            var posterFrame = new View
            {
                WidthSpecification = PosterWidth,
                HeightSpecification = PosterHeight,
                BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f),
                CornerRadius = 16.0f,
                ClippingMode = ClippingModeType.ClipChildren
            };
            var poster = new ImageView
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                ResourceUrl = posterUrl,
                PreMultipliedAlpha = false
            };
            posterFrame.Add(poster);
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
            var titleText = _mediaItem.ItemType == "Episode" ? $"{_mediaItem.SeriesName} - {_mediaItem.Name}" : _mediaItem.Name;
            var title = CreateDetailsTitleView(titleText);
            var overview = new TextLabel(
                string.IsNullOrEmpty(_mediaItem.Overview)
                    ? "No overview available."
                    : _mediaItem.Overview
            )
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 360,
                PointSize = 32,
                TextColor = new Color(0.85f, 0.85f, 0.85f, 1f),
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word
            };
            _infoColumn.Add(title);
            _infoColumn.Add(overview);
            if (_mediaItem.ItemType == "Movie" || _mediaItem.ItemType == "Episode")
            {
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
                    var resumeText = $"Resume ({FormatResumeTime(_mediaItem.PlaybackPositionTicks)})";
                    _resumeButton = CreateActionButton(resumeText, isPrimary: false);
                }

                _subtitleButton = CreateActionButton("Subtitles: Off", isPrimary: false);

                _versionButton = CreateActionButton("Default", isPrimary: false);
                RebuildActionButtons(includeVersionButton: false);
                _infoColumn.Add(_buttonGroup);
            }
            content.Add(posterFrame);
            content.Add(_infoColumn);
            root.Add(backdrop);
            root.Add(dimOverlay);
            root.Add(content);
            Add(root);
        }
        public override void OnShow()
        {
            if (_mediaItem.ItemType == "Series")
            {
                _ = LoadEpisodesAsync();
            }
            else
            {
                _ = LoadSubtitleStreamsAsync();
                _ = LoadMediaSourcesAsync();
                FocusButton(0);
            }
        }

        private View CreateDetailsTitleView(string fallbackText)
        {
            // Episodes should always keep textual title.
            if (_mediaItem.ItemType == "Episode" || !_mediaItem.HasLogo)
                return CreateDetailsTitleLabel(fallbackText);

            var logoUrl = AppState.GetItemLogoUrl(_mediaItem.Id, 960);
            if (string.IsNullOrWhiteSpace(logoUrl))
                return CreateDetailsTitleLabel(fallbackText);

            var logoContainer = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 140,
                ClippingMode = ClippingModeType.ClipChildren
            };

            var logo = new ImageView
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                ResourceUrl = logoUrl,
                PreMultipliedAlpha = false,
                FittingMode = FittingModeType.ShrinkToFit,
                SamplingMode = SamplingModeType.BoxThenLanczos
            };

            logoContainer.Add(logo);
            return logoContainer;
        }

        private static TextLabel CreateDetailsTitleLabel(string text)
        {
            return new TextLabel(text)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                PointSize = 64,
                TextColor = Color.White,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word
            };
        }
        private async Task LoadEpisodesAsync()
        {
            var loading = new TextLabel("Loading episodes...")
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                PointSize = 32,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _infoColumn.Add(loading);
            _episodes = await AppState.Jellyfin.GetEpisodesAsync(_mediaItem.Id);
            _infoColumn.Remove(loading);
            _episodesView = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical
                }
            };
            var episodesScrollView = new ScrollableBase
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent
            };
            episodesScrollView.Add(_episodesView);
            
            int currentSeason = -1;
            foreach (var episode in _episodes)
            {
                if (episode.ParentIndexNumber != currentSeason)
                {
                    currentSeason = episode.ParentIndexNumber;
                    var seasonHeader = new TextLabel($"Season {currentSeason}")
                    {
                        PointSize = 40,
                        TextColor = Color.White,
                        WidthResizePolicy = ResizePolicyType.FillToParent,
                        Margin = new Extents(0, 0, 20, 10)
                    };
                    _episodesView.Add(seasonHeader);
                }
                var episodeView = new View
                {
                    WidthResizePolicy = ResizePolicyType.FillToParent,
                    HeightSpecification = 80,
                    Focusable = true,
                    Padding = new Extents(10, 10, 10, 10),
                    CornerRadius = 24.0f,
                    Margin = new Extents(0, 0, 0, 12)
                };
                var episodeLabel = new TextLabel($"{episode.IndexNumber}. {episode.Name}")
                {
                    PointSize = 30,
                    TextColor = Color.White,
                    WidthResizePolicy = ResizePolicyType.FillToParent,
                    HeightResizePolicy = ResizePolicyType.FillToParent,
                    VerticalAlignment = VerticalAlignment.Center
                };
                episodeView.Add(episodeLabel);
                _episodesView.Add(episodeView);
                _episodeViews.Add(episodeView);
            }
            _infoColumn.Add(episodesScrollView);
            _isEpisodeViewFocused = true;
            FocusEpisode(0);
        }

        private async Task LoadSubtitleStreamsAsync()
        {
            try
            {
                _subtitleStreams = await AppState.Jellyfin.GetSubtitleStreamsAsync(_mediaItem.Id);
                UpdateSubtitleButtonText();
            }
            catch
            {
                // Ignore errors, just no subs
            }
        }

        private async Task LoadMediaSourcesAsync()
        {
            try
            {
                var playbackInfo = await AppState.Jellyfin.GetPlaybackInfoAsync(_mediaItem.Id);
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
                    Id = _mediaItem.Id,
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
            if (_isEpisodeViewFocused)
            {
                HandleEpisodeKey(key);
                return;
            }
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
                case AppKey.Down:
                    if (_mediaItem.ItemType == "Series" && _episodes != null)
                    {
                        _isEpisodeViewFocused = true;
                        FocusEpisode(0);
                    }
                    break;
            }
        }
        private void HandleEpisodeKey(AppKey key)
        {
            switch (key)
            {
                case AppKey.Up:
                    MoveEpisodeFocus(-1);
                    break;
                case AppKey.Down:
                    MoveEpisodeFocus(1);
                    break;
                case AppKey.Enter:
                    PlayMedia(_episodes[_episodeIndex], 0);
                    break;
                case AppKey.Back:
                    NavigationService.NavigateBack();
                    break;
            }
        }
        private void MoveEpisodeFocus(int delta)
        {
            if (_episodeViews.Count == 0) return;
            var newIndex = Math.Clamp(_episodeIndex + delta, 0, _episodeViews.Count - 1);
            FocusEpisode(newIndex);
        }
        private void FocusEpisode(int index)
        {
            if (_episodeIndex >= 0 && _episodeIndex < _episodeViews.Count)
            {
                _episodeViews[_episodeIndex].BackgroundColor = Color.Transparent;
            }
            _episodeIndex = index;
            if (_episodeIndex >= 0 && _episodeIndex < _episodeViews.Count)
            {
                // Lighter grey for better visibility, no border to keep corners rounded
                _episodeViews[_episodeIndex].BackgroundColor = new Color(0.35f, 0.35f, 0.35f, 1.0f);
                FocusManager.Instance.SetCurrentFocusView(_episodeViews[_episodeIndex]);
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
                
                // Add focused look
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
                PlayMedia(_mediaItem, 0);
                return;
            }
            if (_resumeAvailable && _buttonIndex == 1)
            {
                PlayMedia(_mediaItem, TicksToMs(_mediaItem.PlaybackPositionTicks));
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
                // Select first
                _selectedSubtitleIndex = _subtitleStreams[0].Index;
            }
            else
            {
                // Find current index in list
                int currentListIndex = _subtitleStreams.FindIndex(s => s.Index == _selectedSubtitleIndex);
                if (currentListIndex == -1 || currentListIndex == _subtitleStreams.Count - 1)
                {
                    _selectedSubtitleIndex = null; // Back to Off
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
                // Capitalize first letter
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
