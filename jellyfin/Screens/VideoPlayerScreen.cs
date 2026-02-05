using System;
using System.Collections.Generic;
using Tizen.Multimedia;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using Tizen.Applications;
using JellyfinTizen.Core;
using JellyfinTizen.Models;

namespace JellyfinTizen.Screens
{
    public class VideoPlayerScreen : ScreenBase, IKeyHandler
    {
        private Player _player;
        private JellyfinMovie _movie;
        private int _startPositionMs;
        private bool _initialSeekDone = false;
        private View _osd;
        private View _topOsd;
        private TextLabel _playPauseText;
        private bool _osdVisible;
        private bool _isSeeking;
        private int _seekPreviewMs;
        private Timer _osdTimer;
        private View _progressTrack;
        private View _progressFill;
        private TextLabel _currentTimeLabel;
        private TextLabel _durationLabel;
        private View _previewFill;
        private Timer _progressTimer;
        private View _audioOverlay;
        private PlayerTrackInfo _audioTrackInfo;
        private int _audioIndex;
        private bool _audioOverlayVisible;
        private View _subtitleOverlay;
        private PlayerTrackInfo _subtitleTrackInfo;
        private int _subtitleIndex;
        private bool _subtitleOverlayVisible;
        private bool _subtitleEnabled = true;
        private TextLabel _subtitleText;
        private Timer _subtitleHideTimer;
        private Timer _reportProgressTimer;
        private bool _isFinished;

        // New OSD Controls
        private View _controlsContainer;
        private View _prevButton;
        private View _nextButton;
        private int _osdFocusRow = 0; // 0 = Seekbar, 1 = Buttons
        private int _buttonFocusIndex = 1; // 0 = Prev, 1 = Next (Default to Next)
        private bool _showRemaining = false;

        public VideoPlayerScreen(JellyfinMovie movie, int startPositionMs = 0)
        {
            _movie = movie;
            _startPositionMs = startPositionMs;

            // Required for Tizen.Multimedia.Display with NUI Window.
            Window.Default.BackgroundColor = Color.Transparent;
            BackgroundColor = Color.Transparent;
        }

        public override void OnShow()
        {
            if (_osd == null)
                CreateOSD();

            _isFinished = false;
            _reportProgressTimer = new Timer(5000); // 5 seconds
            _reportProgressTimer.Tick += OnReportProgressTick;
            _reportProgressTimer.Start();

            StartPlayback();
        }

        public override void OnHide()
        {
            _reportProgressTimer?.Stop();
            _reportProgressTimer = null;

            // Final progress report
            ReportProgressToServer();

            StopPlayback();
            Window.Default.BackgroundColor = Color.Black;
            BackgroundColor = Color.Black;
        }

        private async void StartPlayback()
{
    try
    {
        _player = new Player();
        
        // FIX 1: Subscribe to the native Completion Event
        _player.PlaybackCompleted += OnPlaybackCompleted;
        _player.SubtitleUpdated += OnSubtitleUpdated;

        // ... (Keep existing Display/Source/Prepare logic) ...
        _player.Display = new Display(Window.Default);
        _player.DisplaySettings.Mode = PlayerDisplayMode.LetterBox;
        _player.DisplaySettings.IsVisible = true;

        var apiKey = Uri.EscapeDataString(AppState.AccessToken);
        var serverUrl = AppState.Jellyfin.ServerUrl;
        var streamUrl = $"{serverUrl}/Videos/{_movie.Id}/stream?static=true&api_key={apiKey}";

        _player.SetSource(new MediaUriSource(streamUrl));
        await _player.PrepareAsync();

        if (_startPositionMs > 0)
        {
            await _player.SetPlayPositionAsync(_startPositionMs, false);
        }

        _player.Start();
    }
    catch (Exception ex)
    {
        Console.WriteLine("PLAYER ERROR: " + ex);
    }
}


private void CreateOSD()
{
    // DEFINITIONS: Tune these to your liking
    int sidePadding = 60;    
    int labelWidth = 140;    
    int labelGap = 20;       
    int bottomHeight = 140;
    int topHeight = 100;
    int screenWidth = Window.Default.Size.Width;

    // --- TOP OSD ---
    _topOsd = new View
    {
        WidthResizePolicy = ResizePolicyType.FillToParent,
        HeightSpecification = topHeight,
        BackgroundColor = new Color(0, 0, 0, 0.1f),
        PositionY = 0
    };
    _topOsd.Hide();

    string titleText = _movie.ItemType == "Episode"
        ? $"{_movie.SeriesName} S{_movie.ParentIndexNumber}:E{_movie.IndexNumber} - {_movie.Name}"
        : _movie.Name;

    var titleLabel = new TextLabel(titleText)
    {
        PositionX = sidePadding,
        WidthResizePolicy = ResizePolicyType.FillToParent,
        HeightResizePolicy = ResizePolicyType.FillToParent,
        PointSize = 34,
        TextColor = Color.White,
        HorizontalAlignment = HorizontalAlignment.Begin,
        VerticalAlignment = VerticalAlignment.Center
    };
    _topOsd.Add(titleLabel);
    Add(_topOsd);

    // --- BOTTOM OSD ---
    _osd = new View
    {
        WidthResizePolicy = ResizePolicyType.FillToParent,
        HeightSpecification = bottomHeight,
        BackgroundColor = new Color(0, 0, 0, 0.1f),
        PositionY = Window.Default.Size.Height - bottomHeight
    };
    _osd.Hide();

    // --- 1. PROGRESS ROW (Manual Positioning) ---
    var progressRow = new View
    {
        WidthResizePolicy = ResizePolicyType.FillToParent,
        HeightSpecification = 50,
        PositionY = 10, 
    };

    // Current Time (Left)
    _currentTimeLabel = new TextLabel("00:00")
    {
        PositionX = sidePadding,
        WidthSpecification = labelWidth,
        HeightResizePolicy = ResizePolicyType.FillToParent,
        PointSize = 24,
        TextColor = Color.White,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Begin
    };

    // Total Duration (Right) - Pinned exactly to the right side
    _durationLabel = new TextLabel("00:00")
    {
        PositionX = screenWidth - sidePadding - labelWidth,
        WidthSpecification = labelWidth,
        HeightResizePolicy = ResizePolicyType.FillToParent,
        PointSize = 24,
        TextColor = Color.White,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.End
    };

    _durationLabel.TouchEvent += (o, e) => 
    {
        if (e.Touch.GetState(0) == PointStateType.Up) { _showRemaining = !_showRemaining; UpdateProgress(); }
        return true; 
    };

    // The Track (Middle) - Calculated exactly
    int trackStartX = sidePadding + labelWidth + labelGap;
    // We calculate width manually so it NEVER changes during layout updates
    int trackWidth = screenWidth - (2 * trackStartX);

    _progressTrack = new View
    {
        PositionX = trackStartX,
        WidthSpecification = trackWidth, 
        HeightSpecification = 6,
        BackgroundColor = new Color(1, 1, 1, 0.3f),
        PositionY = 22, 
        CornerRadius = 3.0f
    };

    _progressFill = new View { HeightSpecification = 6, BackgroundColor = new Color(0, 164f/255f, 220f/255f, 1f), WidthSpecification = 0, CornerRadius = 3.0f }; 
    _previewFill = new View { HeightSpecification = 6, BackgroundColor = new Color(1, 1, 1, 0.6f), WidthSpecification = 0, CornerRadius = 3.0f };

    _progressTrack.Add(_progressFill);
    _progressTrack.Add(_previewFill);

    progressRow.Add(_currentTimeLabel);
    progressRow.Add(_progressTrack);
    progressRow.Add(_durationLabel);

    // --- 2. CONTROLS ROW ---
    _controlsContainer = new View
    {
        WidthResizePolicy = ResizePolicyType.FillToParent,
        HeightSpecification = 80,
        PositionY = 60,
        Layout = new LinearLayout
        {
            LinearOrientation = LinearLayout.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            CellPadding = new Size2D(50, 0)
        }
    };

    _prevButton = CreateOsdButton("Prev");
    _nextButton = CreateOsdButton("Next");

    if (_movie.ItemType == "Episode")
    {
        _controlsContainer.Add(_prevButton);
        _controlsContainer.Add(_nextButton);
    }

    _osd.Add(progressRow);
    _osd.Add(_controlsContainer);
    Add(_osd);

    // ... (Keep your existing overlay/timer setup) ...
    CreateAudioOverlay();
    CreateSubtitleOverlay();
    CreateSubtitleText();
    
    _osdTimer = new Timer(5000);
    _osdTimer.Tick += (_, __) => { HideOSD(); return false; };
    _progressTimer = new Timer(500);
    _progressTimer.Tick += (_, __) => { UpdateProgress(); return true; };
}

        private View CreateOsdButton(string text)
        {
            var btn = new View
            {
                WidthSpecification = 160,
                HeightSpecification = 60,
                BackgroundColor = new Color(1, 1, 1, 0.15f),
                CornerRadius = 30.0f
            };
            var label = new TextLabel(text)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextColor = Color.White,
                PointSize = 24
            };
            btn.Add(label);
            return btn;
        }

private void UpdateProgress()
{
    if (_player == null || _progressTrack == null) return;

    int duration = GetDuration();
    if (duration <= 0) return;

    // FIX: Stabilize Startup
    // If we have a start position (resume), and the player is still near 0,
    // force the UI to show the Resume position. This stops the "0 -> Resume" visual jump.
    int rawPos = _isSeeking ? _seekPreviewMs : GetPlayPositionMs();
    
    if (!_isSeeking && !_initialSeekDone && _startPositionMs > 0)
    {
        // If rawPos is still small (buffering), keep showing _startPositionMs
        if (Math.Abs(rawPos - _startPositionMs) > 2000 && rawPos < 2000)
        {
            rawPos = _startPositionMs;
        }
        else
        {
            // Player has caught up or moved past it
            _initialSeekDone = true;
        }
    }

    int position = Math.Clamp(rawPos, 0, duration);

    // 1. Calculate Ratio
    float ratio = (float)position / duration;

    // 2. Use FIXED Width Math (Matches CreateOSD)
    int sidePadding = 60;
    int labelWidth = 140;
    int labelGap = 20;
    int trackStartX = sidePadding + labelWidth + labelGap;
    int totalTrackWidth = Window.Default.Size.Width - (2 * trackStartX);

    int fillWidth = (int)(totalTrackWidth * ratio);

    if (_isSeeking) {
        _previewFill.WidthSpecification = fillWidth;
        _progressFill.WidthSpecification = 0;
    } else {
        _progressFill.WidthSpecification = fillWidth;
        _previewFill.WidthSpecification = 0;
    }

    _currentTimeLabel.Text = FormatTime(position);
    
    if (_showRemaining)
        _durationLabel.Text = "-" + FormatTime(Math.Max(0, duration - position));
    else
        _durationLabel.Text = FormatTime(duration);
}

        private string FormatTime(int ms)
        {
            var t = TimeSpan.FromMilliseconds(ms);
            return t.Hours > 0
                ? $"{t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}"
                : $"{t.Minutes:D2}:{t.Seconds:D2}";
        }

        private void BeginSeek()
        {
            if (_player == null)
                return;

            _isSeeking = true;
            _seekPreviewMs = GetPlayPositionMs();
        }

        private void Scrub(int seconds)
        {
            if (!_isSeeking)
                BeginSeek();

            _seekPreviewMs += seconds * 1000;
            _seekPreviewMs = Math.Clamp(_seekPreviewMs, 0, GetDuration());

            UpdatePreviewBar();
            ShowOSD();
        }

        private void CommitSeek()
        {
            if (!_isSeeking)
                return;

            _ = _player.SetPlayPositionAsync(_seekPreviewMs, false);
            _isSeeking = false;
        }

        private int GetDuration()
        {
            if (_movie != null && _movie.RunTimeTicks > 0)
            {
                return (int)(_movie.RunTimeTicks / 10000);
            }

            if (_player == null)
                return 0;

            try
            {
                if (_player.StreamInfo == null)
                    return 0;

                int raw = _player.StreamInfo.GetDuration();

                // Heuristic: Some Tizen versions return seconds, others milliseconds.
                // A 38.56 min video is 2313 seconds. If raw is < 36000 (10 hours in seconds),
                // and we're not expecting an extremely short clip, it's likely seconds.
                // We increase the threshold significantly.
                if (raw > 0 && raw < 86400) // 24 hours in seconds
                {
                    // If it were milliseconds, 86400 is only 86 seconds.
                    // This heuristic assumes any video > 86 seconds will be > 86400 if in MS.
                    // But wait, if a video is 1 minute, raw = 60 (sec) or 60000 (ms).
                    // Let's use a more robust check:
                    if (raw < 100000) // Less than 100 seconds if it were MS
                    {
                        // If it's less than 100, it's almost certainly seconds (unless it's a very tiny clip)
                        int ms = raw * 1000;
                        return ms;
                    }
                }

                return raw;
            }
            catch (InvalidOperationException)
            {
                // Player not ready (Preparing) — treat as unknown duration for now
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("GetDuration error: " + ex);
                return 0;
            }
        }

        private int GetPlayPositionMs()
        {
            if (_player == null)
                return 0;

            try
            {
                int rawPos = _player.GetPlayPosition();

                return rawPos;
            }
            catch (InvalidOperationException)
            {
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("GetPlayPositionMs error: " + ex);
                return 0;
            }
        }

        private void UpdatePreviewBar()
        {
            if (_progressTrack == null)
                return;

            int dur = GetDuration();
            if (dur <= 0)
                return;

            float ratio = (float)_seekPreviewMs / dur;
            ratio = Math.Clamp(ratio, 0f, 1f);

            _previewFill.WidthSpecification =
                (int)Math.Floor(_progressTrack.Size.Width * ratio);
        }

        private void CreateSubtitleOverlay()
        {
            _subtitleOverlay = new View
            {
                WidthSpecification = (int)(Window.Default.Size.Width * 0.4f),
                HeightResizePolicy = ResizePolicyType.FillToParent,
                BackgroundColor = new Color(0, 0, 0, 0.85f),
                PositionX = Window.Default.Size.Width * 0.6f,
                PositionY = 0
            };

            _subtitleOverlay.Hide();
            Add(_subtitleOverlay);
        }

        private void CreateSubtitleText()
        {
            _subtitleText = new TextLabel("")
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 120,
                PointSize = 32,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                BackgroundColor = new Color(0, 0, 0, 0.45f),
                PositionY = Window.Default.Size.Height - 300
            };

            _subtitleText.Hide();
            Add(_subtitleText);
        }

        private void ShowSubtitleOverlay()
        {
            if (_player == null)
                return;

            if (_subtitleOverlay == null)
                CreateSubtitleOverlay();

            HideAudioOverlay();
            ClearChildren(_subtitleOverlay);

            _subtitleTrackInfo = _player.SubtitleTrackInfo;
            if (_subtitleTrackInfo == null)
                return;

            int count = _subtitleTrackInfo.GetCount();

            // +1 for "Off"
            if (!_subtitleOverlayVisible)
            {
                try
                {
                    _subtitleIndex = _subtitleTrackInfo.Selected + 1;
                }
                catch (InvalidOperationException)
                {
                    _subtitleIndex = 0; // Default to "Off"
                }
            }

            var listContainer = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                PositionY = 200,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    CellPadding = new Tizen.NUI.Size(0, 10)
                }
            };

            // OFF option
            AddSubtitleRow(listContainer, "OFF", 0 == _subtitleIndex);

            if (count == 0)
            {
                AddSubtitleRow(listContainer, "NO SUBTITLES", false);
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    var lang = _subtitleTrackInfo.GetLanguageCode(i);
                    var label = string.IsNullOrWhiteSpace(lang)
                        ? $"SUB {i + 1}"
                        : lang.ToUpper();

                    AddSubtitleRow(listContainer, label, (i + 1) == _subtitleIndex);
                }
            }

            _subtitleOverlay.Add(listContainer);
            _subtitleOverlay.Show();
            _subtitleOverlayVisible = true;
        }

        private void AddSubtitleRow(View parent, string text, bool selected)
        {
            var row = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 90,
                BackgroundColor = selected
                    ? new Color(1, 1, 1, 0.18f)
                    : Color.Transparent
            };

            var label = new TextLabel(text)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                PointSize = 32,
                TextColor = selected
                    ? Color.White
                    : new Color(1, 1, 1, 0.6f),
                HorizontalAlignment = HorizontalAlignment.Begin,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Extents(48, 48, 0, 0)
            };

            row.Add(label);
            parent.Add(row);
        }

        private void MoveSubtitleSelection(int delta)
        {
            if (!_subtitleOverlayVisible || _subtitleTrackInfo == null)
                return;

            int max = _subtitleTrackInfo.GetCount();
            _subtitleIndex = Math.Clamp(_subtitleIndex + delta, 0, max);

            ShowSubtitleOverlay();
        }

        private void SelectSubtitle()
        {
            if (_subtitleTrackInfo == null)
                return;

            if (_subtitleIndex == 0)
            {
                _subtitleEnabled = false;
                try
                {
                    _player?.ClearSubtitle();
                }
                catch
                {
                    // Some platforms don't allow clearing embedded subtitle tracks.
                }

                RunOnUiThread(() =>
                {
                    _subtitleHideTimer?.Stop();
                    _subtitleText?.Hide();
                });
            }
            else
            {
                _subtitleEnabled = true;
                _subtitleTrackInfo.Selected = _subtitleIndex - 1;
            }

            HideSubtitleOverlay();
        }

        private void CreateAudioOverlay()
        {
            _audioOverlay = new View
            {
                WidthSpecification = (int)(Window.Default.Size.Width * 0.4f),
                HeightResizePolicy = ResizePolicyType.FillToParent,
                BackgroundColor = new Color(0, 0, 0, 0.85f),

                // RIGHT-SIDE PANEL
                PositionX = Window.Default.Size.Width * 0.6f,
                PositionY = 0
            };

            _audioOverlay.Hide();
            Add(_audioOverlay);
        }

        private void ShowAudioOverlay()
        {
            if (_player == null)
                return;

            if (_audioOverlay == null)
                CreateAudioOverlay();

            HideSubtitleOverlay();
            ClearChildren(_audioOverlay);

            _audioTrackInfo = _player.AudioTrackInfo;
            if (_audioTrackInfo == null)
                return;

            int count = _audioTrackInfo.GetCount();
            if (count <= 0)
                return;

            if (!_audioOverlayVisible)
                _audioIndex = Math.Clamp(_audioTrackInfo.Selected, 0, count - 1);

            var listContainer = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                PositionY = 200
            };
            listContainer.Layout = new LinearLayout
            {
                LinearOrientation = LinearLayout.Orientation.Vertical,
                CellPadding = new Tizen.NUI.Size(0, 10)
            };

            for (int i = 0; i < count; i++)
            {
                var lang = _audioTrackInfo.GetLanguageCode(i);
                var langLabel = string.IsNullOrWhiteSpace(lang)
                    ? "UNKNOWN"
                    : lang.ToUpper();

                var row = new View
                {
                    WidthResizePolicy = ResizePolicyType.FillToParent,
                    HeightSpecification = 90,
                    BackgroundColor = (i == _audioIndex)
                        ? new Color(1, 1, 1, 0.18f)
                        : Color.Transparent
                };

                var label = new TextLabel($"{langLabel}  *  Track {i + 1}")
                {
                    WidthResizePolicy = ResizePolicyType.FillToParent,
                    HeightResizePolicy = ResizePolicyType.FillToParent,
                    PointSize = 32,
                    TextColor = (i == _audioIndex)
                        ? Color.White
                        : new Color(1, 1, 1, 0.6f),
                    HorizontalAlignment = HorizontalAlignment.Begin,
                    VerticalAlignment = VerticalAlignment.Center,
                    Padding = new Extents(48, 48, 0, 0)
                };

                row.Add(label);
                listContainer.Add(row);
            }

            _audioOverlay.Add(listContainer);
            _audioOverlay.Show();
            _audioOverlayVisible = true;
        }

        private void HideAudioOverlay()
        {
            if (_audioOverlay == null)
                return;

            _audioOverlay.Hide();
            _audioOverlayVisible = false;
        }

        private void HideSubtitleOverlay()
        {
            if (_subtitleOverlay == null)
                return;

            _subtitleOverlay.Hide();
            _subtitleOverlayVisible = false;
        }

        private void OnSubtitleUpdated(object sender, SubtitleUpdatedEventArgs e)
        {
            RunOnUiThread(() =>
            {
                if (_subtitleText == null)
                    return;

                if (!_subtitleEnabled)
                {
                    _subtitleText.Hide();
                    return;
                }

                if (string.IsNullOrWhiteSpace(e.Text))
                {
                    _subtitleText.Hide();
                    return;
                }

                _subtitleText.Text = e.Text;
                _subtitleText.Show();

                _subtitleHideTimer?.Stop();
                _subtitleHideTimer = new Timer(Math.Max(500, e.Duration));
                _subtitleHideTimer.Tick += (_, __) =>
                {
                    _subtitleText.Hide();
                    return false;
                };
                _subtitleHideTimer.Start();
            });
        }

        private void RunOnUiThread(Action action)
        {
            try
            {
                CoreApplication.Post(action);
            }
            catch
            {
                action();
            }
        }

        private void ClearChildren(View view)
        {
            if (view == null)
                return;

            while (view.ChildCount > 0)
            {
                var child = view.GetChildAt(0);
                view.Remove(child);
                child.Dispose();
            }
        }

        private void MoveAudioSelection(int delta)
        {
            if (_audioOverlay == null || !_audioOverlayVisible || _audioTrackInfo == null)
                return;

            int count = _audioTrackInfo.GetCount();
            if (count <= 0)
                return;

            _audioIndex = Math.Clamp(_audioIndex + delta, 0, count - 1);
            ShowAudioOverlay();
        }

        private void SelectAudioTrack()
        {
            if (_audioTrackInfo == null)
                return;

            int count = _audioTrackInfo.GetCount();
            if (count <= 0)
                return;

            _audioTrackInfo.Selected = _audioIndex;
            HideAudioOverlay();
        }



        private void ShowOSD()
        {
            if (!_osdVisible)
            {
                _osdFocusRow = 0;
            }

            _osd.Show();
            _osd.Opacity = 1;
            if (_topOsd != null)
            {
                _topOsd.Show();
                _topOsd.Opacity = 1;
            }
            _osdVisible = true;

            UpdateOsdFocus();
            UpdateProgress();

            _osdTimer.Stop();
            _osdTimer.Start();

            _progressTimer.Start();
        }

        private void UpdateOsdFocus()
        {
            // Row 0: Seekbar
            if (_osdFocusRow == 0)
            {
                _progressTrack.BackgroundColor = new Color(1, 1, 1, 0.4f); // Highlight track
                _prevButton.BackgroundColor = new Color(1, 1, 1, 0.15f);
                _nextButton.BackgroundColor = new Color(1, 1, 1, 0.15f);
                _prevButton.Scale = Vector3.One;
                _nextButton.Scale = Vector3.One;
            }
            // Row 1: Buttons
            else
            {
                _progressTrack.BackgroundColor = new Color(1, 1, 1, 0.25f); // Dim track

                bool prevFocused = _buttonFocusIndex == 0;
                _prevButton.BackgroundColor = prevFocused ? new Color(0.85f, 0.11f, 0.11f, 1f) : new Color(1, 1, 1, 0.15f);
                _prevButton.Scale = prevFocused ? new Vector3(1.1f, 1.1f, 1f) : Vector3.One;

                bool nextFocused = _buttonFocusIndex == 1;
                _nextButton.BackgroundColor = nextFocused ? new Color(0.85f, 0.11f, 0.11f, 1f) : new Color(1, 1, 1, 0.15f);
                _nextButton.Scale = nextFocused ? new Vector3(1.1f, 1.1f, 1f) : Vector3.One;
            }
        }

        private void HideOSD()
        {
            _osd.Hide();
            _osd.Opacity = 0;
            if (_topOsd != null)
            {
                _topOsd.Hide();
                _topOsd.Opacity = 0;
            }
            _osdVisible = false;

            _osdTimer.Stop();
            _progressTimer.Stop();

            // RESET SEEK STATE (CRITICAL FIX)
            if (_isSeeking && _player != null)
            {
                _isSeeking = false;
                _seekPreviewMs = GetPlayPositionMs();
                UpdateProgress();
            }
        }


        private void UpdatePlayPauseText()
        {
            if (_player.State == PlayerState.Playing)
                _playPauseText.Text = "Pause";
            else
                _playPauseText.Text = "Play";
        }

        private async void SeekBy(int seconds)
        {
            if (_player == null)
                return;

            var state = _player.State;
            if (state != PlayerState.Playing && state != PlayerState.Paused)
                return;

            try
            {
                int pos = 0;
                try
                {
                    pos = GetPlayPositionMs();
                }
                catch
                {
                    // Player may be preparing; fallback to 0
                    pos = 0;
                }

                var newPos = pos + (seconds * 1000);

                var duration = GetDuration();

                if (newPos < 0)
                    newPos = 0;
                if (duration > 0 && newPos > duration)
                    newPos = duration;

                await _player.SetPlayPositionAsync(newPos, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine("SEEK ERROR: " + ex.Message);
            }
        }

        private void DumpAudioTracks()
        {
            var track = _player.AudioTrackInfo;
            if (track == null)
            {
                Console.WriteLine("Audio track info not available.");
                return;
            }

            var count = track.GetCount();
            Console.WriteLine($"Audio tracks found: {count}");

            for (int i = 0; i < count; i++)
            {
                var lang = track.GetLanguageCode(i);
                Console.WriteLine($"AudioTrack index={i}, lang={lang}");
            }
        }

        private bool OnReportProgressTick(object sender, Timer.TickEventArgs e)
        {
           ReportProgressToServer();
           return !_isFinished;
        }

        private void ReportProgressToServer()
{
    if (_player == null || _isSeeking || _isFinished) return;
    if (_player.State != PlayerState.Playing) return;

    var positionMs = GetPlayPositionMs();
    var durationMs = GetDuration();
    if (durationMs <= 0) return;

    // Just MARK as watched, do NOT stop playback
    if (((double)positionMs / durationMs) > 0.95)
    {
        if (!_isFinished) // Only mark once
        {
            _ = AppState.Jellyfin.MarkAsPlayedAsync(_movie.Id);
            // _isFinished = true;  <-- REMOVE THIS. Let it keep reporting until the end.
        }
    }
    else
    {
        var positionTicks = (long)positionMs * 10000;
        _ = AppState.Jellyfin.UpdatePlaybackPositionAsync(_movie.Id, positionTicks);
    }
}

        private void OnPlaybackCompleted(object sender, EventArgs e)
{
    // This fires ONLY when the video naturally ends (100%)
    _isFinished = true;
    _ = AppState.Jellyfin.MarkAsPlayedAsync(_movie.Id);

    RunOnUiThread(() =>
    {
        // Auto-play next episode logic
        if (_movie.ItemType == "Episode")
        {
            PlayNextEpisode();
        }
        else
        {
            NavigationService.NavigateBack();
        }
    });
}

        private void StopPlayback()
        {
            try
            {
                if (_player == null)
                    return;

                // Stop timers to prevent UpdateProgress from accessing player during teardown
                try { _progressTimer?.Stop(); } catch { }
                try { _osdTimer?.Stop(); } catch { }

                _player.SubtitleUpdated -= OnSubtitleUpdated;
                _subtitleHideTimer?.Stop();
                _subtitleText?.Hide();

                try { _player.Stop(); } catch { }
                try { _player.Unprepare(); } catch { }
                try { _player.Dispose(); } catch { }
                _player = null;
            }
            catch { }
        }

        public void HandleKey(AppKey key)
        {
            switch (key)
            {
                case AppKey.Enter:
                    if (_audioOverlayVisible)
                        SelectAudioTrack();
                    else if (_subtitleOverlayVisible)
                        SelectSubtitle();
                    else if (_isSeeking)
                        CommitSeek();
                    else if (_osdVisible)
                    {
                        if (_osdFocusRow == 1)
                            ActivateOsdButton();
                        else
                        {
                            TogglePause();
                            // Keep OSD visible and reset timer
                            _osdTimer.Stop();
                            _osdTimer.Start();
                        }
                    }
                    else
                    {
                        ShowOSD();
                    }
                    break;

                case AppKey.Left:
                    if (_audioOverlayVisible)
                        MoveAudioSelection(-1);
                    else if (_osdVisible && _osdFocusRow == 1)
                        MoveButtonFocus(-1);
                    else
                        Scrub(-30);
                    break;

                case AppKey.Right:
                    if (_audioOverlayVisible)
                        MoveAudioSelection(1);
                    else if (_osdVisible && _osdFocusRow == 1)
                        MoveButtonFocus(1);
                    else
                        Scrub(30);
                    break;

                case AppKey.Up:
                    if (_audioOverlayVisible)
                       MoveAudioSelection(-1);
                    else if (_subtitleOverlayVisible)
                       MoveSubtitleSelection(-1);
                    else if (_osdVisible)
                       MoveOsdRow(-1);
                    else
                       ShowAudioOverlay(); // later we can cycle menus
                    break;

                case AppKey.Down:
                    if (_audioOverlayVisible)
                       MoveAudioSelection(1);
                    else if (_subtitleOverlayVisible)
                       MoveSubtitleSelection(1);
                    else if (_osdVisible)
                       MoveOsdRow(1);
                    else
                       ShowSubtitleOverlay();
                       break;

                case AppKey.Back:
                    if (_subtitleOverlayVisible)
                        HideSubtitleOverlay();
                    else if (_audioOverlayVisible)
                        HideAudioOverlay();
                    else if (_isSeeking)
                        _isSeeking = false;
                    else if (_osdVisible)
                        HideOSD();
                    else
                        NavigationService.NavigateBack();
                    break;
            }
        }

        private void MoveOsdRow(int delta)
        {
            // 0 = Seek, 1 = Buttons
            int newRow = Math.Clamp(_osdFocusRow + delta, 0, 1);
            if (newRow != _osdFocusRow)
            {
                _osdFocusRow = newRow;
                UpdateOsdFocus();
                _osdTimer.Stop(); // Reset hide timer
                _osdTimer.Start();
            }
        }

        private void MoveButtonFocus(int delta)
        {
            int newIndex = Math.Clamp(_buttonFocusIndex + delta, 0, 1);
            if (newIndex != _buttonFocusIndex)
            {
                _buttonFocusIndex = newIndex;
                UpdateOsdFocus();
                _osdTimer.Stop();
                _osdTimer.Start();
            }
        }

        private void ActivateOsdButton()
        {
            if (_buttonFocusIndex == 0)
                PlayPreviousEpisode();
            else
                PlayNextEpisode();
        }

        private async void PlayNextEpisode()
        {
            await SwitchEpisode(1);
        }

        private async void PlayPreviousEpisode()
        {
            // Typical UX: if playback has progressed beyond a short threshold,
            // pressing "Prev" restarts the current episode; otherwise it goes to previous episode.
            const int restartThresholdMs = 30 * 1000; // 30 seconds

            if (_player != null)
            {
                int pos = 0;
                try
                {
                    pos = _player.GetPlayPosition();
                }
                catch
                {
                    pos = 0;
                }

                if (pos > restartThresholdMs)
                {
                    try
                    {
                        await _player.SetPlayPositionAsync(0, false);
                        _isSeeking = false;
                        _seekPreviewMs = 0;
                        UpdateProgress();
                    }
                    catch { }
                    return;
                }
            }

            await SwitchEpisode(-1);
        }

        private async System.Threading.Tasks.Task SwitchEpisode(int offset)
        {
            if (_movie.ItemType != "Episode")
                return;

            try
            {
                // Show loading indicator or status
                if (_playPauseText != null) _playPauseText.Text = "Loading...";
                StopPlayback();

                // 1. Get Seasons for this Series
                var seasons = await AppState.Jellyfin.GetSeasonsAsync(_movie.SeriesId);
                if (seasons == null || seasons.Count == 0)
                {
                    Console.WriteLine("No seasons found for series.");
                    NavigationService.NavigateBack();
                    return;
                }

                // Ensure seasons are ordered by IndexNumber
                seasons.Sort((a, b) => a.IndexNumber.CompareTo(b.IndexNumber));

                // Find current season
                var currentSeason = seasons.Find(s => s.IndexNumber == _movie.ParentIndexNumber);
                if (currentSeason == null)
                {
                    // Fallback: use first season if index-based lookup fails
                    currentSeason = seasons[0];
                }

                if (currentSeason == null)
                {
                    Console.WriteLine("Could not find current season.");
                    NavigationService.NavigateBack();
                    return;
                }

                // 2. Get Episodes for the current Season
                var episodes = await AppState.Jellyfin.GetEpisodesAsync(currentSeason.Id) ?? new List<JellyfinMovie>();
                var currentIndex = episodes.FindIndex(e => e.Id == _movie.Id);

                // If current episode not found in this season, try to locate across seasons
                if (currentIndex == -1)
                {
                    for (int si = 0; si < seasons.Count; si++)
                    {
                        var eps = await AppState.Jellyfin.GetEpisodesAsync(seasons[si].Id) ?? new List<JellyfinMovie>();
                        var idx = eps.FindIndex(e => e.Id == _movie.Id);
                        if (idx != -1)
                        {
                            currentSeason = seasons[si];
                            episodes = eps;
                            currentIndex = idx;
                            break;
                        }
                    }

                    if (currentIndex == -1)
                    {
                        Console.WriteLine("Current episode not found in any season.");
                        NavigationService.NavigateBack();
                        return;
                    }
                }

                var nextIndex = currentIndex + offset;

                // If within same season
                if (nextIndex >= 0 && nextIndex < episodes.Count)
                {
                    var nextEpisode = episodes[nextIndex];
                    LoadNewMedia(nextEpisode);
                    return;
                }

                // Cross-season navigation
                int currentSeasonPos = seasons.FindIndex(s => s.Id == currentSeason.Id);
                if (offset > 0)
                {
                    // Moving forward: find next season with episodes
                    for (int si = currentSeasonPos + 1; si < seasons.Count; si++)
                    {
                        var nextSeasonEpisodes = await AppState.Jellyfin.GetEpisodesAsync(seasons[si].Id) ?? new List<JellyfinMovie>();
                        if (nextSeasonEpisodes.Count > 0)
                        {
                            LoadNewMedia(nextSeasonEpisodes[0]); // first episode of next season
                            return;
                        }
                    }
                }
                else
                {
                    // Moving backward: find previous season with episodes
                    for (int si = currentSeasonPos - 1; si >= 0; si--)
                    {
                        var prevSeasonEpisodes = await AppState.Jellyfin.GetEpisodesAsync(seasons[si].Id) ?? new List<JellyfinMovie>();
                        if (prevSeasonEpisodes.Count > 0)
                        {
                            LoadNewMedia(prevSeasonEpisodes[prevSeasonEpisodes.Count - 1]); // last episode of previous season
                            return;
                        }
                    }
                }

                // No more episodes in requested direction
                Console.WriteLine("No more episodes in this direction.");
                NavigationService.NavigateBack();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error switching episode: {ex.Message}");
                NavigationService.NavigateBack();
            }
        }

        private void LoadNewMedia(JellyfinMovie newMovie)
{
    _movie = newMovie;
    _isFinished = false;
    _isSeeking = false;
    _seekPreviewMs = 0;
    
    // FIX: Use resume position if available
    if (newMovie.PlaybackPositionTicks > 0)
        _startPositionMs = (int)(newMovie.PlaybackPositionTicks / 10000);
    else
        _startPositionMs = 0;

    _initialSeekDone = false;

    // Reset UI
    _progressFill.WidthSpecification = 0;
    _currentTimeLabel.Text = "00:00";
    _durationLabel.Text = "00:00";

    _osdFocusRow = 0;
    _buttonFocusIndex = 1;
    UpdateOsdFocus();
    
    StartPlayback();
    ShowOSD();
}

        private void TogglePause()
        {
            if (_player == null)
                return;

            if (_player.State == PlayerState.Playing)
                _player.Pause();
            else if (_player.State == PlayerState.Paused)
                _player.Start();
        }
    }
}