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
        private readonly List<View> _buttons = new();
        private int _buttonIndex;
        private View _infoColumn;
        private View _episodesView;
        private List<JellyfinMovie> _episodes;
        private readonly List<View> _episodeViews = new();
        private int _episodeIndex = -1;
        private bool _isEpisodeViewFocused;

        private List<MediaStream> _subtitleStreams;
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
                BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f)
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
            var title = new TextLabel(titleText)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                PointSize = 64,
                TextColor = Color.White,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word
            };
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
                var buttonRow = new View
                {
                    WidthResizePolicy = ResizePolicyType.FillToParent,
                    HeightSpecification = 100,
                    Layout = new LinearLayout
                    {
                        LinearOrientation = LinearLayout.Orientation.Horizontal,
                        CellPadding = new Size2D(20, 0)
                    },
                    Margin = new Extents(0, 0, 10, 0)
                };
                _playButton = CreateActionButton("Play", isPrimary: true);
                _buttons.Add(_playButton);
                buttonRow.Add(_playButton);
                if (_resumeAvailable)
                {
                    var resumeText = $"Resume ({FormatResumeTime(_mediaItem.PlaybackPositionTicks)})";
                    _resumeButton = CreateActionButton(resumeText, isPrimary: false);
                    _buttons.Add(_resumeButton);
                    buttonRow.Add(_resumeButton);
                }

            _subtitleButton = CreateActionButton("Subtitles: Off", isPrimary: false);
            _buttons.Add(_subtitleButton);
            buttonRow.Add(_subtitleButton);

                _infoColumn.Add(buttonRow);
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
                FocusButton(0);
            }
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
                    Padding = new Extents(10, 10, 10, 10)
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
                _episodeViews[_episodeIndex].BackgroundColor = new Color(0.2f, 0.2f, 0.2f, 1);
                FocusManager.Instance.SetCurrentFocusView(_episodeViews[_episodeIndex]);
            }
        }
        private View CreateActionButton(string text, bool isPrimary)
        {
            var button = new View
            {
                WidthSpecification = 340,
                HeightSpecification = 90,
                BackgroundColor = isPrimary
                    ? new Color(0.85f, 0.11f, 0.11f, 1f)
                    : new Color(0.2f, 0.2f, 0.2f, 1f),
                Focusable = true
            };
            var label = new TextLabel(text)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                PointSize = 34,
                TextColor = Color.White
            };
            button.Add(label);
            return button;
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
                _buttons[i].Scale = focused ? new Vector3(1.05f, 1.05f, 1f) : Vector3.One;
                _buttons[i].Opacity = focused ? 1f : 0.85f;
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

        private void PlayMedia(JellyfinMovie media, int startPositionMs)
        {
            NavigationService.Navigate(
                new VideoPlayerScreen(media, startPositionMs, _selectedSubtitleIndex, AppState.BurnInSubtitles)
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