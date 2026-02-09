using System;
using System.Collections.Generic;
using Tizen.Multimedia;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using Tizen.NUI.Components;
using Tizen.Applications;
using JellyfinTizen.Core;
using JellyfinTizen.Models;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http;
using System.Linq;

namespace JellyfinTizen.Screens
{
    public class VideoPlayerScreen : ScreenBase, IKeyHandler
    {
        private Player _player;
        private JellyfinMovie _movie;
        private int _startPositionMs;
        private bool _initialSeekDone = false;
        private View _debugOverlay;
        private TextLabel _debugText;
        private View _osd;
        private View _topOsd;
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
        private bool _subtitleEnabled;
        private TextLabel _subtitleText;
        private Timer _subtitleHideTimer;
        private Timer _reportProgressTimer;
        private bool _isFinished;
        private View _progressThumb;
        private View _subtitleListContainer;
        private ScrollableBase _subtitleScrollView;
        private View _audioListContainer;
        private ScrollableBase _audioScrollView;
        private string _playMethod = "DirectPlay";
        private string _playSessionId;
        private int? _initialSubtitleIndex;
        private bool _burnIn;
        
        // Track if we have external subtitles
        private bool _hasExternalSubtitle = false;
        private string _externalSubtitlePath = null;
        private string _externalSubtitleLanguage = "EXTERNAL"; 
        private int? _externalSubtitleIndex = null;
        private string _externalSubtitleMediaSourceId = null;
        private string _externalSubtitleCodec = null;

        // OSD Controls
        private View _controlsContainer;
        private View _prevButton;
        private View _nextButton;
        private int _osdFocusRow = 0; // 0 = Seekbar, 1 = Buttons
        private int _buttonFocusIndex = 1; 

        // --- NEW: Store MediaSource and Override Audio ---
        private MediaSourceInfo _currentMediaSource;
        private int? _overrideAudioIndex = null;

        public VideoPlayerScreen(JellyfinMovie movie, int startPositionMs = 0, int? subtitleStreamIndex = null, bool burnIn = false)
        {
            _movie = movie;
            _startPositionMs = startPositionMs;
            _initialSubtitleIndex = subtitleStreamIndex;
            _burnIn = burnIn;

            // CRITICAL: Ensure the window is transparent so the video plane is visible.
            Window.Default.BackgroundColor = Color.Transparent;
            BackgroundColor = Color.Transparent;
        }

        private void CreateDebugOverlay()
        {
            if (_debugOverlay != null) return;

            _debugOverlay = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 500,
                BackgroundColor = new Color(0, 0, 0, 0.7f),
                Position = new Position(0, 0),
                ParentOrigin = Tizen.NUI.ParentOrigin.TopLeft,
                PivotPoint = Tizen.NUI.PivotPoint.TopLeft,
            };

            _debugText = new TextLabel
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                TextColor = Color.Yellow,
                PointSize = 14,
                MultiLine = true,
                HorizontalAlignment = HorizontalAlignment.Begin,
                VerticalAlignment = VerticalAlignment.Top,
                Text = "Initializing Debug Log..."
            };

            _debugText.Padding = new Extents(20, 20, 20, 20);
            _debugOverlay.Add(_debugText);
            Add(_debugOverlay);
        }

        public void Log(string message)
        {
            Console.WriteLine($"[JELLYFIN_PLAYER] {message}");
            Tizen.Applications.CoreApplication.Post(() =>
            {
                if (_debugText != null)
                {
                    string current = _debugText.Text;
                    var lines = current.Split('\n');
                    if (lines.Length > 15) current = string.Join("\n", lines, 0, 15);
                    _debugText.Text = $"[{DateTime.Now:HH:mm:ss}] {message}\n" + current;
                }
            });
        }

        public override void OnShow()
        {
            if (_osd == null) CreateOSD();

            _isFinished = false;
            _reportProgressTimer = new Timer(5000);
            _reportProgressTimer.Tick += OnReportProgressTick;
            _reportProgressTimer.Start();

            StartPlayback();
        }

        public override void OnHide()
        {
            _reportProgressTimer?.Stop();
            _reportProgressTimer = null;
            ReportProgressToServer();
            StopPlayback();
            Window.Default.BackgroundColor = Color.Black;
            BackgroundColor = Color.Black;
        }

        private async void StartPlayback()
        {
            try
            {
                if (_debugOverlay == null) CreateDebugOverlay();
                Log($"Initializing Player for: {_movie.Name}");

                _subtitleEnabled = _initialSubtitleIndex.HasValue;
                _player = new Player();
                
                _player.ErrorOccurred += (s, e) => Log($"!!! PLAYER ERROR !!!: {e.Error}");
                _player.BufferingProgressChanged += (s, e) => { if (e.Percent % 20 == 0) Log($"Buffering: {e.Percent}%"); };
                _player.PlaybackCompleted += OnPlaybackCompleted;
                _player.SubtitleUpdated += OnSubtitleUpdated;

                _player.Display = new Display(Window.Default);
                _player.DisplaySettings.Mode = PlayerDisplayMode.LetterBox;
                _player.DisplaySettings.IsVisible = true;

                Log("Fetching PlaybackInfo...");
                var playbackInfo = await AppState.Jellyfin.GetPlaybackInfoAsync(_movie.Id, _initialSubtitleIndex, _burnIn);
                
                if (playbackInfo.MediaSources == null || playbackInfo.MediaSources.Count == 0)
                {
                    Log("Error: No MediaSources found.");
                    return;
                }

                var mediaSource = playbackInfo.MediaSources[0];
                _currentMediaSource = mediaSource; 
                _playSessionId = playbackInfo.PlaySessionId;
                
                // Log Streams
                foreach (var stream in mediaSource.MediaStreams)
                {
                     Log($"Stream: Type={stream.Type}, Index={stream.Index}, Codec={stream.Codec}, External={stream.IsExternal}");
                }
                
                Log($"Supports DirectPlay: {mediaSource.SupportsDirectPlay}");
                Log($"Supports Transcoding: {mediaSource.SupportsTranscoding}");
                Log($"Transcoding URL: {mediaSource.TranscodingUrl}");

                string streamUrl = "";
                var apiKey = AppState.AccessToken;
                var serverUrl = AppState.Jellyfin.ServerUrl;

                // --- ROBUST PLAYBACK SELECTION LOGIC ---
                
                bool supportsDirectPlay = mediaSource.SupportsDirectPlay;
                bool supportsTranscoding = mediaSource.SupportsTranscoding;
                bool hasTranscodeUrl = !string.IsNullOrEmpty(mediaSource.TranscodingUrl);
                
                // Reasons to force transcoding:
                // 1. Audio Override is active (e.g. DTS selected via UI)
                // 2. Burn-In is requested for subtitles
                bool forceTranscode = _overrideAudioIndex.HasValue || (_burnIn && _initialSubtitleIndex.HasValue);

                // --- DIRECT PLAY LOGIC ---
                if (supportsDirectPlay && (!forceTranscode || !supportsTranscoding))
                {
                    _playMethod = "DirectPlay";
                    streamUrl = $"{serverUrl}/Videos/{_movie.Id}/stream?static=true&MediaSourceId={mediaSource.Id}&PlaySessionId={_playSessionId}&api_key={apiKey}";
                    
                    if (forceTranscode) Log("WARNING: Forced Transcode requested but not available. Falling back to DirectPlay.");
                    Log("Mode: DirectPlay");
                }
                // --- TRANSCODE / DIRECT STREAM LOGIC ---
                else if (supportsTranscoding || forceTranscode)
                {
                    _playMethod = "Transcode";

                    // SMART CONTAINER SELECTION:
                    var videoStream = mediaSource.MediaStreams.FirstOrDefault(s => s.Type == "Video");
                    string vidCodec = videoStream?.Codec?.ToLower() ?? "unknown";
                    
                    // Native Video Codecs for Tizen
                    bool isVideoNative = vidCodec.Contains("h264") || vidCodec.Contains("hevc") || 
                                         vidCodec.Contains("vp9") || vidCodec.Contains("av1");

                    // 1. Direct Stream (Video is Native + No Burn-in) -> MP4 (Lighter, better scrubbing)
                    // 2. Full Transcode / Burn-in -> TS (Better stability for heavy processing)
                    string container = (isVideoNative && !_burnIn) ? "mp4" : "ts";
                    
                    // Audio Priority: AC3 > EAC3 > AAC > MP3
                    string audioPriority = "ac3,eac3,aac,mp3";
                    
                    // FIX: If server didn't provide a URL (because it thought DP was fine), we must build one manually.
                    if (!hasTranscodeUrl)
                    {
                        Log($"Constructing Manual URL (Container: {container}, Audio: AC3+)...");
                        streamUrl = $"{serverUrl}/Videos/{_movie.Id}/master.m3u8?MediaSourceId={mediaSource.Id}&PlaySessionId={_playSessionId}&api_key={apiKey}";
                        
                        streamUrl += "&VideoCodec=h264,hevc,vp9,av1";
                        streamUrl += $"&AudioCodec={audioPriority}"; 
                        streamUrl += $"&SegmentContainer={container}"; 
                        streamUrl += "&TranscodingMaxAudioChannels=6";
                        streamUrl += "&MinSegments=1";
                        streamUrl += "&BreakOnNonKeyFrames=True";
                    }
                    else
                    {
                        streamUrl = $"{serverUrl}{mediaSource.TranscodingUrl}";
                        
                        // Inject preferences into existing URL if we are overriding
                        if (_overrideAudioIndex.HasValue)
                        {
                            if (!streamUrl.Contains("SegmentContainer=")) streamUrl += $"&SegmentContainer={container}";
                            if (!streamUrl.Contains("AudioCodec=")) streamUrl += $"&AudioCodec={audioPriority}";
                        }
                    }

                    string AppendParam(string url, string param) 
                    {
                        if (url.Contains("?")) { if (url.EndsWith("?") || url.EndsWith("&")) return $"{url}{param}"; return $"{url}&{param}"; }
                        return $"{url}?{param}";
                    }

                    if (!streamUrl.Contains("api_key=") && !streamUrl.Contains("Token=")) streamUrl = AppendParam(streamUrl, $"api_key={apiKey}");
                    if (!streamUrl.Contains("PlaySessionId=") && !string.IsNullOrEmpty(_playSessionId)) streamUrl = AppendParam(streamUrl, $"PlaySessionId={_playSessionId}");
                    
                    // Apply Audio Override
                    if (_overrideAudioIndex.HasValue)
                    {
                        Log($"Applying Audio Override Index: {_overrideAudioIndex.Value}");
                        streamUrl = AppendParam(streamUrl, $"AudioStreamIndex={_overrideAudioIndex.Value}");
                    }

                    // Apply Subtitles
                    if (_initialSubtitleIndex.HasValue)
                    {
                        if (!streamUrl.Contains("SubtitleStreamIndex=")) streamUrl = AppendParam(streamUrl, $"SubtitleStreamIndex={_initialSubtitleIndex}");
                        if (_burnIn && !streamUrl.Contains("SubtitleMethod=")) streamUrl = AppendParam(streamUrl, "SubtitleMethod=Encode");
                    }
                    
                    streamUrl = streamUrl.Replace("?&", "?").Replace("&&", "&").Replace(" ", "%20").Replace("\n", "").Replace("\r", "");
                    
                    Log($"Mode: Transcode/DirectStream (Config: {container})");
                }
                else
                {
                    Log("Error: Format not supported by Server.");
                    return;
                }

                // --- SUBTITLE TRACKING & EXTRACTION ---
                if (!_burnIn)
                {
                    var externalSubStreams = mediaSource.MediaStreams?.Where(s => s.Type == "Subtitle" && s.IsExternal).ToList();
                    if (externalSubStreams != null && externalSubStreams.Count > 0)
                    {
                        var subStream = externalSubStreams.First();
                        _hasExternalSubtitle = true;
                        _externalSubtitleIndex = subStream.Index;
                        _externalSubtitleMediaSourceId = mediaSource.Id;
                        _externalSubtitleCodec = subStream.Codec;
                        _externalSubtitleLanguage = !string.IsNullOrEmpty(subStream.Language) ? subStream.Language.ToUpper() : "EXTERNAL";
                        Log($"Found external subtitle stream: {_externalSubtitleLanguage}");
                    }
                    else
                    {
                        var internalSubStreams = mediaSource.MediaStreams?.Where(s => s.Type == "Subtitle" && !s.IsExternal).ToList();
                        if (internalSubStreams != null && internalSubStreams.Count > 0)
                        {
                            // For HLS transcodes, internal subtitles often fail to load in Tizen.
                            // We track them so we can extract them as sidecar files if needed.
                            if (_playMethod == "Transcode")
                            {
                                var subStream = internalSubStreams.First();
                                _hasExternalSubtitle = true;
                                _externalSubtitleIndex = subStream.Index;
                                _externalSubtitleMediaSourceId = mediaSource.Id;
                                _externalSubtitleCodec = subStream.Codec;
                                _externalSubtitleLanguage = !string.IsNullOrEmpty(subStream.Language) ? subStream.Language.ToUpper() : "EXTERNAL";
                                Log($"Found internal subtitle (treated as external for HLS): {_externalSubtitleLanguage}");
                            }
                            else
                            {
                                Log($"Found internal subtitle streams: Count = {internalSubStreams.Count}");
                            }
                        }
                    }
                    
                    if (_initialSubtitleIndex.HasValue)
                    {
                        var subStream = mediaSource.MediaStreams?.FirstOrDefault(s => s.Type == "Subtitle" && s.Index == _initialSubtitleIndex.Value);
                        if (subStream != null)
                        {
                            _hasExternalSubtitle = true;
                            _externalSubtitleIndex = _initialSubtitleIndex.Value;
                            _externalSubtitleMediaSourceId = mediaSource.Id;
                            _externalSubtitleCodec = subStream.Codec;
                            _externalSubtitleLanguage = !string.IsNullOrEmpty(subStream.Language) ? subStream.Language.ToUpper() : "EXTERNAL";
                        }
                    }
                }

                Log($"URL: {streamUrl}");

                var source = new MediaUriSource(streamUrl);
                _player.SetSource(source);

                // --- DOWNLOAD SUBTITLE IF NEEDED ---
                if (!_burnIn && _initialSubtitleIndex.HasValue)
                {
                    try 
                    {
                        var subStream = mediaSource.MediaStreams?.FirstOrDefault(s => s.Type == "Subtitle" && s.Index == _initialSubtitleIndex.Value);
                        // If External OR Transcoding (Extract Internal), download it.
                        if (subStream != null && (subStream.IsExternal || _playMethod == "Transcode"))
                        {
                            await DownloadAndSetSubtitle(mediaSource.Id, _initialSubtitleIndex.Value, subStream.Codec);
                        }
                    }
                    catch (Exception subEx) { Log($"Subtitle Error: {subEx.Message}"); }
                }

                Log("Calling PrepareAsync...");
                await _player.PrepareAsync();
                Log("PrepareAsync Success.");

                // Check Track Detection
                try { var v = _player.StreamInfo.GetVideoProperties(); Log($"Video: {v.Size.Width}x{v.Size.Height}"); } catch {}
                try { Log($"Audio Tracks: {_player.AudioTrackInfo.GetCount()}"); } catch {}
                try 
                {
                    int subCount = _player.SubtitleTrackInfo.GetCount();
                    Log($"Subtitle Tracks: {subCount}");

                    // Map Jellyfin Index to Tizen Index (for DirectPlay)
                    if (_initialSubtitleIndex.HasValue && subCount > 0)
                    {
                        var embeddedSubs = mediaSource.MediaStreams?
                            .Where(s => s.Type == "Subtitle" && !s.IsExternal)
                            .OrderBy(s => s.Index)
                            .ToList();

                        if (embeddedSubs != null)
                        {
                            int tizenIndex = embeddedSubs.FindIndex(s => s.Index == _initialSubtitleIndex.Value);
                            if (tizenIndex != -1 && tizenIndex < subCount)
                            {
                                Log($"Mapping StreamIndex {_initialSubtitleIndex} to Tizen Track {tizenIndex}");
                                _player.SubtitleTrackInfo.Selected = tizenIndex;
                                _subtitleIndex = tizenIndex + 1;
                                _subtitleEnabled = true;
                            }
                        }
                    }
                } catch {}

                if (_startPositionMs > 0)
                {
                    Log($"Resuming at {_startPositionMs}ms");
                    await _player.SetPlayPositionAsync(_startPositionMs, false);
                }

                _player.Start();
                Log("Playback Started.");

                var info = new PlaybackProgressInfo
                {
                    ItemId = _movie.Id, PlaySessionId = _playSessionId, MediaSourceId = mediaSource.Id,
                    PositionTicks = _startPositionMs * 10000, IsPaused = false, PlayMethod = _playMethod, EventName = "TimeUpdate"
                };
                _ = AppState.Jellyfin.ReportPlaybackStartAsync(info);
            }
            catch (Exception ex)
            {
                Log($"CRITICAL START EXCEPTION: {ex.Message}");
            }
        }

        private async Task DownloadAndSetSubtitle(string mediaSourceId, int subtitleIndex, string codec)
        {
            try
            {
                string ext = "vtt"; 
                if (!string.IsNullOrEmpty(codec))
                {
                    var c = codec.ToLowerInvariant();
                    if (c.Contains("srt") || c.Contains("subrip")) ext = "srt";
                    else if (c.Contains("ass") || c.Contains("ssa")) ext = "ssa";
                }

                var apiKey = AppState.AccessToken;
                var serverUrl = AppState.Jellyfin.ServerUrl;
                
                // /Videos/{ItemId}/{MediaSourceId}/Subtitles/{Index}/0/Stream.{Format}
                var downloadUrl = $"{serverUrl}/Videos/{_movie.Id}/{mediaSourceId}/Subtitles/{subtitleIndex}/0/Stream.{ext}?api_key={apiKey}";
                Log($"Downloading Subtitle: {downloadUrl}");

                var localPath = System.IO.Path.Combine(Application.Current.DirectoryInfo.Data, $"sub_{mediaSourceId}_{subtitleIndex}.{ext}");

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("X-Emby-Authorization", $"MediaBrowser Client=\"JellyfinTizen\", Device=\"SamsungTV\", DeviceId=\"tizen-tv\", Version=\"1.0\", Token=\"{apiKey}\"");
                    var response = await client.GetAsync(downloadUrl);
                    response.EnsureSuccessStatusCode();
                    var data = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(localPath, data);
                }

                Log($"Setting Subtitle Path: {localPath}");
                _player.SetSubtitle(localPath);
                
                _hasExternalSubtitle = true;
                _externalSubtitlePath = localPath;
            }
            catch (Exception ex)
            {
                Log($"Failed to set subtitle: {ex.Message}");
            }
        }

        private void CreateOSD()
        {
            int sidePadding = 60; int labelWidth = 140; int labelGap = 20; int bottomHeight = 260; int topHeight = 160; int screenWidth = Window.Default.Size.Width;

            _topOsd = new View { WidthResizePolicy = ResizePolicyType.FillToParent, HeightSpecification = topHeight, PositionY = 0 };
            var topGradient = new PropertyMap();
            topGradient.Add(Visual.Property.Type, new PropertyValue((int)Visual.Type.Gradient));
            topGradient.Add(GradientVisualProperty.StartPosition, new PropertyValue(new Vector2(0.0f, -0.5f)));
            topGradient.Add(GradientVisualProperty.EndPosition, new PropertyValue(new Vector2(0.0f, 0.5f)));
            var topOffsets = new PropertyArray();
            topOffsets.Add(new PropertyValue(0.0f));
            topOffsets.Add(new PropertyValue(1.0f));
            topGradient.Add(GradientVisualProperty.StopOffset, new PropertyValue(topOffsets));
            var topColors = new PropertyArray();
            topColors.Add(new PropertyValue(new Color(0, 0, 0, 0.9f)));
            topColors.Add(new PropertyValue(new Color(0, 0, 0, 0.0f)));
            topGradient.Add(GradientVisualProperty.StopColor, new PropertyValue(topColors));
            _topOsd.Background = topGradient;
            _topOsd.Hide();

            string titleText = _movie.ItemType == "Episode" ? $"{_movie.SeriesName} S{_movie.ParentIndexNumber}:E{_movie.IndexNumber} - {_movie.Name}" : _movie.Name;
            var titleLabel = new TextLabel(titleText) { PositionX = sidePadding, PositionY = 40, WidthResizePolicy = ResizePolicyType.FillToParent, HeightResizePolicy = ResizePolicyType.FillToParent, PointSize = 34, TextColor = Color.White, HorizontalAlignment = HorizontalAlignment.Begin, VerticalAlignment = VerticalAlignment.Top };
            _topOsd.Add(titleLabel);
            Add(_topOsd);

            _osd = new View { WidthResizePolicy = ResizePolicyType.FillToParent, HeightSpecification = bottomHeight, PositionY = Window.Default.Size.Height - bottomHeight };
            var bottomGradient = new PropertyMap();
            bottomGradient.Add(Visual.Property.Type, new PropertyValue((int)Visual.Type.Gradient));
            bottomGradient.Add(GradientVisualProperty.StartPosition, new PropertyValue(new Vector2(0.0f, -0.5f)));
            bottomGradient.Add(GradientVisualProperty.EndPosition, new PropertyValue(new Vector2(0.0f, 0.5f)));
            var bottomOffsets = new PropertyArray();
            bottomOffsets.Add(new PropertyValue(0.0f));
            bottomOffsets.Add(new PropertyValue(1.0f));
            bottomGradient.Add(GradientVisualProperty.StopOffset, new PropertyValue(bottomOffsets));
            var bottomColors = new PropertyArray();
            bottomColors.Add(new PropertyValue(new Color(0, 0, 0, 0.0f)));
            bottomColors.Add(new PropertyValue(new Color(0, 0, 0, 0.95f)));
            bottomGradient.Add(GradientVisualProperty.StopColor, new PropertyValue(bottomColors));
            _osd.Background = bottomGradient;
            _osd.Hide();

            var progressRow = new View { WidthResizePolicy = ResizePolicyType.FillToParent, HeightSpecification = 50, PositionY = 110 };
            _currentTimeLabel = new TextLabel("00:00") { PositionX = sidePadding, WidthSpecification = labelWidth, HeightResizePolicy = ResizePolicyType.FillToParent, PointSize = 24, TextColor = Color.White, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Begin };
            _durationLabel = new TextLabel("00:00") { PositionX = screenWidth - sidePadding - labelWidth, WidthSpecification = labelWidth, HeightResizePolicy = ResizePolicyType.FillToParent, PointSize = 24, TextColor = Color.White, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.End };

            int trackStartX = sidePadding + labelWidth + labelGap;
            int trackWidth = screenWidth - (2 * trackStartX);

            _progressTrack = new View { PositionX = trackStartX, WidthSpecification = trackWidth, HeightSpecification = 6, BackgroundColor = new Color(1, 1, 1, 0.3f), PositionY = 22, CornerRadius = 3.0f };
            _progressFill = new View { HeightSpecification = 6, BackgroundColor = new Color(0, 164f/255f, 220f/255f, 1f), WidthSpecification = 0, CornerRadius = 3.0f }; 
            _previewFill = new View { HeightSpecification = 6, BackgroundColor = new Color(1, 1, 1, 0.6f), WidthSpecification = 0, CornerRadius = 3.0f };
            _progressTrack.Add(_progressFill);
            _progressTrack.Add(_previewFill);

            _progressThumb = new View { WidthSpecification = 24, HeightSpecification = 24, BackgroundColor = Color.White, CornerRadius = 12.0f, PositionY = 13, PositionX = trackStartX };

            progressRow.Add(_currentTimeLabel);
            progressRow.Add(_progressTrack);
            progressRow.Add(_durationLabel);
            progressRow.Add(_progressThumb);

            _controlsContainer = new View { WidthResizePolicy = ResizePolicyType.FillToParent, HeightSpecification = 80, PositionY = 170, Layout = new LinearLayout { LinearOrientation = LinearLayout.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, CellPadding = new Size2D(50, 0) } };
            _prevButton = CreateOsdButton("Prev");
            _nextButton = CreateOsdButton("Next");

            if (_movie.ItemType == "Episode") { _controlsContainer.Add(_prevButton); _controlsContainer.Add(_nextButton); }

            _osd.Add(progressRow);
            _osd.Add(_controlsContainer);
            Add(_osd);

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
            var btn = new View { WidthSpecification = 160, HeightSpecification = 60, BackgroundColor = new Color(1, 1, 1, 0.15f), CornerRadius = 30.0f };
            var label = new TextLabel(text) { WidthResizePolicy = ResizePolicyType.FillToParent, HeightResizePolicy = ResizePolicyType.FillToParent, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextColor = Color.White, PointSize = 24 };
            btn.Add(label);
            return btn;
        }

        private void UpdateProgress()
        {
            if (_player == null || _progressTrack == null) return;

            int duration = GetDuration();
            if (duration <= 0) return;

            int rawPos = _isSeeking ? _seekPreviewMs : GetPlayPositionMs();
            if (!_isSeeking && !_initialSeekDone && _startPositionMs > 0)
            {
                if (Math.Abs(rawPos - _startPositionMs) > 2000 && rawPos < 2000) rawPos = _startPositionMs;
                else _initialSeekDone = true;
            }

            int position = Math.Clamp(rawPos, 0, duration);
            float ratio = (float)position / duration;
            int trackStartX = 60 + 140 + 20;
            int totalTrackWidth = Window.Default.Size.Width - (2 * trackStartX);
            int fillWidth = (int)(totalTrackWidth * ratio);

            if (_isSeeking) {
                _previewFill.WidthSpecification = fillWidth;
                _progressFill.WidthSpecification = 0;
                if (_progressThumb != null) _progressThumb.PositionX = trackStartX + fillWidth - 12;
            } else {
                _progressFill.WidthSpecification = fillWidth;
                _previewFill.WidthSpecification = 0;
                if (_progressThumb != null) _progressThumb.PositionX = trackStartX + fillWidth - 12;
            }

            _currentTimeLabel.Text = FormatTime(position);
            _durationLabel.Text = FormatTime(duration);
        }

        private string FormatTime(int ms)
        {
            var t = TimeSpan.FromMilliseconds(ms);
            return t.Hours > 0 ? $"{t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}" : $"{t.Minutes:D2}:{t.Seconds:D2}";
        }

        private void BeginSeek() { if (_player != null) { _isSeeking = true; _seekPreviewMs = GetPlayPositionMs(); } }

        private void Scrub(int seconds)
        {
            if (!_isSeeking) BeginSeek();
            _seekPreviewMs += seconds * 1000;
            _seekPreviewMs = Math.Clamp(_seekPreviewMs, 0, GetDuration());
            UpdatePreviewBar();
            ShowOSD();
        }

        private async void CommitSeek()
        {
            if (!_isSeeking || _player == null) return;
            try
            {
                var seekTask = _player.SetPlayPositionAsync(_seekPreviewMs, false);
                await Task.WhenAny(seekTask, Task.Delay(3000));
            }
            catch {}
            finally { _isSeeking = false; }
        }

        private int GetDuration()
        {
            if (_movie != null && _movie.RunTimeTicks > 0) return (int)(_movie.RunTimeTicks / 10000);
            if (_player == null) return 0;
            try { return _player.StreamInfo.GetDuration(); } catch { return 0; }
        }

        private int GetPlayPositionMs()
        {
            if (_player == null) return 0;
            try { return _player.GetPlayPosition(); } catch { return 0; }
        }

        private void UpdatePreviewBar()
        {
            if (_progressTrack == null) return;
            int dur = GetDuration();
            if (dur <= 0) return;
            float ratio = Math.Clamp((float)_seekPreviewMs / dur, 0f, 1f);
            _previewFill.WidthSpecification = (int)Math.Floor(_progressTrack.Size.Width * ratio);
        }

        private void CreateSubtitleOverlay()
        {
            _subtitleOverlay = new View { WidthSpecification = 450, HeightSpecification = 500, BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.95f), PositionX = Window.Default.Size.Width - 500, PositionY = Window.Default.Size.Height - 780, CornerRadius = 25.0f, ClippingMode = ClippingModeType.ClipChildren };
            _subtitleOverlay.Hide();
            Add(_subtitleOverlay);
        }

        private void CreateSubtitleText()
        {
            _subtitleText = new TextLabel("") { WidthResizePolicy = ResizePolicyType.FillToParent, HeightSpecification = 200, PointSize = 46, TextColor = Color.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, BackgroundColor = Color.Transparent, PositionY = Window.Default.Size.Height - 200, MultiLine = true, LineWrapMode = LineWrapMode.Word, Padding = new Extents(150, 150, 0, 0), EnableMarkup = true };
            var shadow = new PropertyMap(); shadow.Add("offset", new PropertyValue(new Vector2(2, 2))); shadow.Add("color", new PropertyValue(new Color(0, 0, 0, 1.0f)));
            _subtitleText.Shadow = shadow;
            _subtitleText.Hide();
            Add(_subtitleText);
        }

        private void ShowSubtitleOverlay()
        {
            if (_player == null) return;
            if (_subtitleOverlay == null) CreateSubtitleOverlay();
            HideAudioOverlay();

            // 1. Use Jellyfin Metadata to list ALL tracks (fixes Tizen HLS limitation)
            var subtitleStreams = _currentMediaSource?.MediaStreams?
                .Where(s => s.Type == "Subtitle")
                .OrderBy(s => s.Index)
                .ToList();

            if (_subtitleOverlay.ChildCount == 0)
            {
                _subtitleOverlay.Add(new TextLabel("Subtitles") { WidthResizePolicy = ResizePolicyType.FillToParent, HeightSpecification = 80, PointSize = 34, TextColor = Color.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
                _subtitleScrollView = new ScrollableBase { WidthResizePolicy = ResizePolicyType.FillToParent, HeightResizePolicy = ResizePolicyType.FillToParent, PositionY = 80, HeightSpecification = 420, ScrollingDirection = ScrollableBase.Direction.Vertical };
                _subtitleListContainer = new View { WidthResizePolicy = ResizePolicyType.FillToParent, HeightResizePolicy = ResizePolicyType.FitToChildren, Layout = new LinearLayout { LinearOrientation = LinearLayout.Orientation.Vertical, CellPadding = new Size2D(0, 5) } };
                _subtitleScrollView.Add(_subtitleListContainer);
                _subtitleOverlay.Add(_subtitleScrollView);
                
                // --- OFF OPTION ---
                var offRow = CreateSubtitleRow("OFF", "OFF_INDEX");
                _subtitleListContainer.Add(offRow);

                // --- POPULATE TRACKS ---
                if (subtitleStreams != null)
                {
                    foreach (var stream in subtitleStreams)
                    {
                        var lang = !string.IsNullOrEmpty(stream.Language) ? stream.Language.ToUpper() : "UNKNOWN";
                        var title = !string.IsNullOrEmpty(stream.DisplayTitle) ? stream.DisplayTitle : $"Sub {stream.Index}";
                        var labelText = $"{lang} | {title}";
                        if (stream.IsExternal) labelText += " (Ext)";
                        
                        var row = CreateSubtitleRow(labelText, stream.Index.ToString());
                        _subtitleListContainer.Add(row);
                    }
                }
            }

            UpdateSubtitleVisuals();
            _subtitleOverlay.Show();
            _subtitleOverlayVisible = true;
        }

        private View CreateSubtitleRow(string text, string indexId)
        {
            var row = new View { WidthResizePolicy = ResizePolicyType.FillToParent, HeightSpecification = 60, BackgroundColor = Color.Transparent, CornerRadius = 12.0f, Margin = new Extents(20, 20, 5, 5) };
            row.Name = indexId; 
            var label = new TextLabel(text) { WidthResizePolicy = ResizePolicyType.FillToParent, HeightResizePolicy = ResizePolicyType.FillToParent, PointSize = 28, TextColor = new Color(1, 1, 1, 0.6f), HorizontalAlignment = HorizontalAlignment.Begin, VerticalAlignment = VerticalAlignment.Center, Padding = new Extents(20, 0, 0, 0) };
            row.Add(label);
            return row;
        }

        private void UpdateSubtitleVisuals()
        {
            if (_subtitleListContainer == null) return;
            int count = (int)_subtitleListContainer.ChildCount;
            for (int i = 0; i < count; i++)
            {
                var row = _subtitleListContainer.GetChildAt((uint)i);
                bool selected = (i == _subtitleIndex);
                row.BackgroundColor = selected ? new Color(1, 1, 1, 0.2f) : Color.Transparent;
                var label = row.GetChildAt(0) as TextLabel;
                if (label != null) label.TextColor = selected ? Color.White : new Color(1, 1, 1, 0.6f);
            }
        }

        private void MoveSubtitleSelection(int delta)
        {
            if (!_subtitleOverlayVisible || _subtitleListContainer == null) return;
            int count = (int)_subtitleListContainer.ChildCount;
            if (count == 0) return;
            _subtitleIndex = Math.Clamp(_subtitleIndex + delta, 0, count - 1);
            ShowSubtitleOverlay();
        }

        private async void SelectSubtitle()
        {
            if (_subtitleListContainer == null) return;
            if (_subtitleIndex < 0 || _subtitleIndex >= _subtitleListContainer.ChildCount) return;
            var selectedRow = _subtitleListContainer.GetChildAt((uint)_subtitleIndex);
            string selectedName = selectedRow.Name;
            
            HideSubtitleOverlay();

            if (selectedName == "OFF_INDEX")
            {
                _subtitleEnabled = false;
                Log("Subtitles: Disabled");
                try { _player?.ClearSubtitle(); } catch { }
                RunOnUiThread(() => { _subtitleHideTimer?.Stop(); _subtitleText?.Hide(); });
                return;
            }

            if (!int.TryParse(selectedName, out int jellyfinStreamIndex)) return;
            Log($"User selected Subtitle Stream Index: {jellyfinStreamIndex}");
            _subtitleEnabled = true;

            var subStream = _currentMediaSource?.MediaStreams?.FirstOrDefault(s => s.Index == jellyfinStreamIndex);
            if (subStream == null) return;

            // Try Native Switch if DirectPlay + Internal
            bool tryNative = _playMethod == "DirectPlay" && !subStream.IsExternal;

            if (tryNative)
            {
                try 
                {
                    var embeddedSubs = _currentMediaSource.MediaStreams.Where(s => s.Type == "Subtitle" && !s.IsExternal).OrderBy(s => s.Index).ToList();
                    int tizenIndex = embeddedSubs.FindIndex(s => s.Index == jellyfinStreamIndex);
                    if (tizenIndex != -1 && tizenIndex < _player.SubtitleTrackInfo.GetCount())
                    {
                        Log($"Native Switch to Tizen Subtitle Track {tizenIndex}");
                        _player.SubtitleTrackInfo.Selected = tizenIndex;
                        return; 
                    }
                }
                catch {}
            }

            // Fallback: Download and set as sidecar (Works for HLS and External)
            Log("Setting subtitle via Extraction/Download...");
            await DownloadAndSetSubtitle(_currentMediaSource.Id, jellyfinStreamIndex, subStream.Codec);
        }

        private void CreateAudioOverlay()
        {
            _audioOverlay = new View { WidthSpecification = 450, HeightSpecification = 500, BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.95f), PositionX = Window.Default.Size.Width - 500, PositionY = Window.Default.Size.Height - 780, CornerRadius = 25.0f, ClippingMode = ClippingModeType.ClipChildren };
            _audioOverlay.Hide();
            Add(_audioOverlay);
        }

        private void ShowAudioOverlay()
        {
            if (_player == null) return;
            if (_audioOverlay == null) CreateAudioOverlay();
            HideSubtitleOverlay();

            var audioStreams = _currentMediaSource?.MediaStreams?
                .Where(s => s.Type == "Audio")
                .OrderBy(s => s.Index)
                .ToList();

            if (audioStreams == null || audioStreams.Count == 0) return;
            
            if (_audioOverlay.ChildCount == 0)
            {
                _audioOverlay.Add(new TextLabel("Audio Tracks") { WidthResizePolicy = ResizePolicyType.FillToParent, HeightSpecification = 80, PointSize = 34, TextColor = Color.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
                _audioScrollView = new ScrollableBase { WidthResizePolicy = ResizePolicyType.FillToParent, HeightResizePolicy = ResizePolicyType.FillToParent, PositionY = 80, HeightSpecification = 420, ScrollingDirection = ScrollableBase.Direction.Vertical };
                _audioListContainer = new View { WidthResizePolicy = ResizePolicyType.FillToParent, HeightResizePolicy = ResizePolicyType.FitToChildren, Layout = new LinearLayout { LinearOrientation = LinearLayout.Orientation.Vertical, CellPadding = new Size2D(0, 5) } };
                _audioScrollView.Add(_audioListContainer);
                _audioOverlay.Add(_audioScrollView);

                foreach (var stream in audioStreams)
                {
                    var lang = !string.IsNullOrEmpty(stream.Language) ? stream.Language.ToUpper() : "UNKNOWN";
                    var codec = !string.IsNullOrEmpty(stream.Codec) ? stream.Codec.ToUpper() : "AUDIO";
                    var displayText = $"{lang} | {codec}";

                    var row = new View { WidthResizePolicy = ResizePolicyType.FillToParent, HeightSpecification = 60, BackgroundColor = Color.Transparent, CornerRadius = 12.0f, Margin = new Extents(20, 20, 5, 5) };
                    row.Name = stream.Index.ToString();

                    var label = new TextLabel(displayText) { WidthResizePolicy = ResizePolicyType.FillToParent, HeightResizePolicy = ResizePolicyType.FillToParent, PointSize = 26, TextColor = new Color(1, 1, 1, 0.6f), HorizontalAlignment = HorizontalAlignment.Begin, VerticalAlignment = VerticalAlignment.Center, Padding = new Extents(20, 0, 0, 0) };
                    row.Add(label);
                    _audioListContainer.Add(row);
                }
            }
            UpdateAudioVisuals();
            _audioOverlay.Show();
            _audioOverlayVisible = true;
        }

        private void UpdateAudioVisuals()
        {
            if (_audioListContainer == null) return;
            int count = (int)_audioListContainer.ChildCount;
            for (int i = 0; i < count; i++)
            {
                var row = _audioListContainer.GetChildAt((uint)i);
                bool selected = (i == _audioIndex);
                row.BackgroundColor = selected ? new Color(1, 1, 1, 0.2f) : Color.Transparent;
                var label = row.GetChildAt(0) as TextLabel;
                if (label != null) label.TextColor = selected ? Color.White : new Color(1, 1, 1, 0.6f);
            }
        }

        private void HideAudioOverlay() { if (_audioOverlay != null) { _audioOverlay.Hide(); _audioOverlayVisible = false; } }
        private void HideSubtitleOverlay() { if (_subtitleOverlay != null) { _subtitleOverlay.Hide(); _subtitleOverlayVisible = false; } }

        private void OnSubtitleUpdated(object sender, SubtitleUpdatedEventArgs e)
        {
            RunOnUiThread(() =>
            {
                string text = e.Text;
                if (_subtitleText == null || !_subtitleEnabled || string.IsNullOrWhiteSpace(text)) { _subtitleText?.Hide(); return; }
                _subtitleText.Text = text;
                _subtitleText.Show();
                _subtitleHideTimer?.Stop();
                _subtitleHideTimer = new Timer(Math.Max(500, e.Duration));
                _subtitleHideTimer.Tick += (_, __) => { _subtitleText.Hide(); return false; };
                _subtitleHideTimer.Start();
            });
        }

        private void RunOnUiThread(Action action) { try { CoreApplication.Post(action); } catch { action(); } }
        
        private void MoveAudioSelection(int delta) 
        { 
            if (_audioOverlay == null || !_audioOverlayVisible || _audioListContainer == null) return; 
            int count = (int)_audioListContainer.ChildCount;
            if (count <= 0) return; 
            _audioIndex = Math.Clamp(_audioIndex + delta, 0, count - 1); 
            ShowAudioOverlay(); 
        }

        private async void SelectAudioTrack() 
        { 
            if (_audioListContainer == null) return; 
            if (_audioIndex < 0 || _audioIndex >= _audioListContainer.ChildCount) return;
            var selectedRow = _audioListContainer.GetChildAt((uint)_audioIndex);
            
            if (!int.TryParse(selectedRow.Name, out int jellyfinStreamIndex)) return;

            HideAudioOverlay();
            Log($"User selected Audio Stream Index: {jellyfinStreamIndex}");

            var selectedStream = _currentMediaSource?.MediaStreams?.FirstOrDefault(s => s.Index == jellyfinStreamIndex);
            string codec = selectedStream?.Codec?.ToLower() ?? "";
            
            bool isLikelyNative = _playMethod == "DirectPlay" && 
                                  (codec.Contains("aac") || codec.Contains("ac3") || codec.Contains("eac3") || codec.Contains("mp3"));

            if (isLikelyNative)
            {
                try
                {
                    var supportedTracks = _currentMediaSource.MediaStreams
                        .Where(s => s.Type == "Audio" && 
                               (s.Codec.ToLower().Contains("aac") || s.Codec.ToLower().Contains("ac3") || 
                                s.Codec.ToLower().Contains("eac3") || s.Codec.ToLower().Contains("mp3")))
                        .OrderBy(s => s.Index)
                        .ToList();

                    int tizenIndex = supportedTracks.FindIndex(s => s.Index == jellyfinStreamIndex);
                    if (tizenIndex != -1 && tizenIndex < _player.AudioTrackInfo.GetCount())
                    {
                        Log($"Native Switch to Tizen Track {tizenIndex}");
                        _player.AudioTrackInfo.Selected = tizenIndex;
                        return;
                    }
                }
                catch {}
            }

            Log("Native switch not possible (Unsupported Codec or mismatch). Reloading via Server...");
            long currentPos = GetPlayPositionMs();
            StopPlayback();
            _overrideAudioIndex = jellyfinStreamIndex;
            _startPositionMs = (int)currentPos;
            StartPlayback();
        }

        private void ShowOSD()
        {
            if (!_osdVisible) _osdFocusRow = 0;
            _osd.Show(); _osd.Opacity = 1;
            if (_topOsd != null) { _topOsd.Show(); _topOsd.Opacity = 1; }
            _osdVisible = true;
            if (_subtitleText != null) _subtitleText.PositionY = Window.Default.Size.Height - 330;
            UpdateOsdFocus();
            UpdateProgress();
            _osdTimer.Stop(); _osdTimer.Start();
            _progressTimer.Start();
        }

        private void UpdateOsdFocus()
        {
            if (_osdFocusRow == 0)
            {
                _progressTrack.BackgroundColor = new Color(1, 1, 1, 0.4f);
                _prevButton.BackgroundColor = new Color(1, 1, 1, 0.15f); _nextButton.BackgroundColor = new Color(1, 1, 1, 0.15f);
                _prevButton.Scale = Vector3.One; _nextButton.Scale = Vector3.One;
            }
            else
            {
                _progressTrack.BackgroundColor = new Color(1, 1, 1, 0.25f);
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
            _osd.Hide(); _osd.Opacity = 0;
            if (_topOsd != null) { _topOsd.Hide(); _topOsd.Opacity = 0; }
            _osdVisible = false;
            if (_subtitleText != null) _subtitleText.PositionY = Window.Default.Size.Height - 180;
            _osdTimer.Stop();
            _progressTimer.Stop();
            if (_isSeeking && _player != null) { _isSeeking = false; _seekPreviewMs = GetPlayPositionMs(); UpdateProgress(); }
        }

        private bool OnReportProgressTick(object sender, Timer.TickEventArgs e) { ReportProgressToServer(force: false); return true; }

        private void ReportProgressToServer(bool force = false)
        {
            if (_player == null || _isSeeking || _isFinished) return;
            if (!force && _player.State != PlayerState.Playing) return;

            var positionMs = GetPlayPositionMs();
            var durationMs = GetDuration();
            if (durationMs <= 0) return;

            var info = new PlaybackProgressInfo
            {
                ItemId = _movie.Id, PlaySessionId = _playSessionId, MediaSourceId = _movie.Id,
                PositionTicks = (long)positionMs * 10000, IsPaused = _player.State == PlayerState.Paused,
                PlayMethod = _playMethod, EventName = force ? (_player.State == PlayerState.Paused ? "Pause" : "Unpause") : "TimeUpdate"
            };
            _ = AppState.Jellyfin.ReportPlaybackProgressAsync(info);

            if (((double)positionMs / durationMs) > 0.95 && !_isFinished) { _ = AppState.Jellyfin.MarkAsPlayedAsync(_movie.Id); }
            else { _ = AppState.Jellyfin.UpdatePlaybackPositionAsync(_movie.Id, (long)positionMs * 10000); }
        }

        private void OnPlaybackCompleted(object sender, EventArgs e)
        {
            Log("Playback Completed.");
            _isFinished = true;
            _ = AppState.Jellyfin.MarkAsPlayedAsync(_movie.Id);
            RunOnUiThread(() => { if (_movie.ItemType == "Episode") PlayNextEpisode(); else NavigationService.NavigateBack(); });
        }

        private void StopPlayback()
        {
            try 
            {
                if (_player != null) {
                    var info = new PlaybackProgressInfo { ItemId = _movie.Id, PlaySessionId = _playSessionId, PositionTicks = GetPlayPositionMs() * 10000, EventName = "Stop" };
                    _ = AppState.Jellyfin.ReportPlaybackStoppedAsync(info);
                }
            } catch {}
            
            try
            {
                if (_player == null) return;
                try { _progressTimer?.Stop(); } catch { }
                try { _osdTimer?.Stop(); } catch { }
                _player.SubtitleUpdated -= OnSubtitleUpdated;
                _subtitleHideTimer?.Stop();
                _subtitleText?.Hide();
                try { _player.Stop(); } catch { }
                try { _player.Unprepare(); } catch { }
                try { _player.Dispose(); } catch { }
                _player = null;
            } catch { }
        }

        public void HandleKey(AppKey key)
        {
            if (key != AppKey.Unknown) Log($"Key: {key}");
            switch (key)
            {
                case AppKey.MediaPlayPause: TogglePause(); ShowOSD(); break;
                case AppKey.MediaPlay: if (_player != null && _player.State == PlayerState.Paused) { TogglePause(); ShowOSD(); } break;
                case AppKey.MediaPause: if (_player != null && _player.State == PlayerState.Playing) { TogglePause(); ShowOSD(); } break;
                case AppKey.MediaStop: NavigationService.NavigateBack(); break;
                case AppKey.MediaNext: PlayNextEpisode(); break;
                case AppKey.MediaPrevious: PlayPreviousEpisode(); break;
                case AppKey.MediaRewind: Scrub(-10); break;
                case AppKey.MediaFastForward: Scrub(30); break;
                case AppKey.Enter:
                    if (_audioOverlayVisible) SelectAudioTrack();
                    else if (_subtitleOverlayVisible) SelectSubtitle();
                    else if (_isSeeking) CommitSeek();
                    else if (_osdVisible) { if (_osdFocusRow == 1) ActivateOsdButton(); else { TogglePause(); _osdTimer.Stop(); _osdTimer.Start(); } }
                    else ShowOSD();
                    break;
                case AppKey.Left:
                    if (_audioOverlayVisible) MoveAudioSelection(-1);
                    else if (_osdVisible && _osdFocusRow == 1) MoveButtonFocus(-1);
                    else Scrub(-30);
                    break;
                case AppKey.Right:
                    if (_audioOverlayVisible) MoveAudioSelection(1);
                    else if (_osdVisible && _osdFocusRow == 1) MoveButtonFocus(1);
                    else Scrub(30);
                    break;
                case AppKey.Up:
                    if (_audioOverlayVisible) MoveAudioSelection(-1);
                    else if (_subtitleOverlayVisible) MoveSubtitleSelection(-1);
                    else if (_osdVisible) MoveOsdRow(-1);
                    else ShowAudioOverlay();
                    break;
                case AppKey.Down:
                    if (_audioOverlayVisible) MoveAudioSelection(1);
                    else if (_subtitleOverlayVisible) MoveSubtitleSelection(1);
                    else if (_osdVisible) MoveOsdRow(1);
                    else ShowSubtitleOverlay();
                    break;
                case AppKey.Back:
                    if (_subtitleOverlayVisible) HideSubtitleOverlay();
                    else if (_audioOverlayVisible) HideAudioOverlay();
                    else if (_isSeeking) { _isSeeking = false; UpdateProgress(); }
                    else if (_osdVisible) HideOSD();
                    else NavigationService.NavigateBack();
                    break;
            }
        }

        private void MoveOsdRow(int delta) { int newRow = Math.Clamp(_osdFocusRow + delta, 0, 1); if (newRow != _osdFocusRow) { _osdFocusRow = newRow; UpdateOsdFocus(); _osdTimer.Stop(); _osdTimer.Start(); } }
        private void MoveButtonFocus(int delta) { int newIndex = Math.Clamp(_buttonFocusIndex + delta, 0, 1); if (newIndex != _buttonFocusIndex) { _buttonFocusIndex = newIndex; UpdateOsdFocus(); _osdTimer.Stop(); _osdTimer.Start(); } }
        private void ActivateOsdButton() { if (_buttonFocusIndex == 0) PlayPreviousEpisode(); else PlayNextEpisode(); }
        
        private async void PlayNextEpisode() { await SwitchEpisode(1); }
        private async void PlayPreviousEpisode()
        {
            if (_player != null && GetPlayPositionMs() > 30000) { await _player.SetPlayPositionAsync(0, false); _isSeeking = false; _seekPreviewMs = 0; UpdateProgress(); return; }
            await SwitchEpisode(-1);
        }

        private async Task SwitchEpisode(int offset)
        {
            if (_movie.ItemType != "Episode") return;
            try
            {
                Log("Switching Episode...");
                StopPlayback();
                var seasons = await AppState.Jellyfin.GetSeasonsAsync(_movie.SeriesId);
                if (seasons == null || seasons.Count == 0) { NavigationService.NavigateBack(); return; }
                seasons.Sort((a, b) => a.IndexNumber.CompareTo(b.IndexNumber));
                var currentSeason = seasons.Find(s => s.IndexNumber == _movie.ParentIndexNumber) ?? seasons[0];
                var episodes = await AppState.Jellyfin.GetEpisodesAsync(currentSeason.Id) ?? new List<JellyfinMovie>();
                var currentIndex = episodes.FindIndex(e => e.Id == _movie.Id);

                if (currentIndex == -1) 
                {
                    foreach(var s in seasons) { var eps = await AppState.Jellyfin.GetEpisodesAsync(s.Id); var idx = eps.FindIndex(e => e.Id == _movie.Id); if (idx != -1) { currentSeason = s; episodes = eps; currentIndex = idx; break; } }
                }
                
                int nextIndex = currentIndex + offset;
                if (nextIndex >= 0 && nextIndex < episodes.Count) { LoadNewMedia(episodes[nextIndex]); return; }

                int seasonPos = seasons.FindIndex(s => s.Id == currentSeason.Id);
                if (offset > 0) {
                    for (int si = seasonPos + 1; si < seasons.Count; si++) { var eps = await AppState.Jellyfin.GetEpisodesAsync(seasons[si].Id); if (eps.Count > 0) { LoadNewMedia(eps[0]); return; } }
                } else {
                    for (int si = seasonPos - 1; si >= 0; si--) { var eps = await AppState.Jellyfin.GetEpisodesAsync(seasons[si].Id); if (eps.Count > 0) { LoadNewMedia(eps[eps.Count-1]); return; } }
                }
                NavigationService.NavigateBack();
            }
            catch (Exception ex) { Log($"Ep Switch Error: {ex.Message}"); NavigationService.NavigateBack(); }
        }

        private void LoadNewMedia(JellyfinMovie newMovie)
        {
            _movie = newMovie;
            _isFinished = false;
            _isSeeking = false;
            _seekPreviewMs = 0;
            _startPositionMs = newMovie.PlaybackPositionTicks > 0 ? (int)(newMovie.PlaybackPositionTicks / 10000) : 0;
            _initialSeekDone = false;
            _progressFill.WidthSpecification = 0;
            _currentTimeLabel.Text = "00:00"; _durationLabel.Text = "00:00";
            _overrideAudioIndex = null;
            StartPlayback();
            ShowOSD();
        }

        private void TogglePause()
        {
            if (_player == null) return;
            if (_player.State == PlayerState.Playing) { _player.Pause(); ReportProgressToServer(force: true); }
            else if (_player.State == PlayerState.Paused) { _player.Start(); ReportProgressToServer(force: true); }
        }
    }
}