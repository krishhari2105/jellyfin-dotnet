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
        private readonly List<View> _buttons = new();
        private int _buttonIndex;
        private View _infoColumn;

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
                BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f)
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
            var overview = new TextLabel(
                string.IsNullOrEmpty(_episode.Overview)
                    ? "No overview available."
                    : _episode.Overview
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
                var resumeText = $"Resume ({FormatResumeTime(_episode.PlaybackPositionTicks)})";
                _resumeButton = CreateActionButton(resumeText, isPrimary: false);
                _buttons.Add(_resumeButton);
                buttonRow.Add(_resumeButton);
            }
            _infoColumn.Add(buttonRow);
            content.Add(thumbFrame);
            content.Add(_infoColumn);
            root.Add(backdrop);
            root.Add(dimOverlay);
            root.Add(content);
            Add(root);
        }
        public override void OnShow()
        {
            FocusButton(0);
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
                PlayMedia(_episode, 0);
                return;
            }
            if (_resumeAvailable && _buttonIndex == 1)
            {
                PlayMedia(_episode, TicksToMs(_episode.PlaybackPositionTicks));
            }
        }
        private void PlayMedia(JellyfinMovie media, int startPositionMs)
        {
            NavigationService.Navigate(
                new VideoPlayerScreen(media, startPositionMs)
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