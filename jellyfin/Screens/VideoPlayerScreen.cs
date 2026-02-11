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
using System.Text.RegularExpressions;
using System.Net;

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
        private int _audioIndex;
        private bool _audioOverlayVisible;
        private View _subtitleOverlay;
        private int _subtitleIndex;
        private bool _subtitleOverlayVisible;
        private bool _subtitleEnabled;
        private TextLabel _subtitleText;
        private Timer _subtitleHideTimer;
        private Timer _subtitleRenderTimer;
        private List<SubtitleCue> _subtitleCues = new List<SubtitleCue>();
        private bool _useParsedSubtitleRenderer;
        private int _activeSubtitleCueIndex = -1;
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
        private string _preferredMediaSourceId;
        private string _externalSubtitlePath = null;
        private string _externalSubtitleLanguage = "EXTERNAL"; 
        private int? _externalSubtitleIndex = null;
        private string _externalSubtitleMediaSourceId = null;
        private string _externalSubtitleCodec = null;

        // OSD Controls
        private View _controlsContainer;
        private View _subtitleOffsetButton;
        private View _prevButton;
        private View _nextButton;
        private View _subtitleOffsetTrackContainer;
        private View _subtitleOffsetThumb;
        private View _subtitleOffsetCenterMarker;
        private bool _subtitleOffsetAdjustMode;
        private int _subtitleOffsetMs;
        private bool _subtitleOffsetBurnInWarningShown;
        private int _osdButtonCount = 1;
        private int _osdFocusRow = 0; // 0 = Seekbar, 1 = Buttons
        private int _buttonFocusIndex = 0;

        private const int OffsetButtonIndex = 0;
        private const int PrevButtonIndex = 1;
        private const int NextButtonIndex = 2;
        private const int SubtitleOffsetStepMs = 100;
        private const int SubtitleOffsetLimitMs = 5000;
        private const int SubtitleOffsetTrackWidth = 280;

        // --- NEW: Store MediaSource and Override Audio ---
        private MediaSourceInfo _currentMediaSource;
        private int? _overrideAudioIndex = null;
        private bool _isAnamorphicVideo;

        private struct SubtitleCue
        {
            public int StartMs;
            public int EndMs;
            public string Text;
        }

        public VideoPlayerScreen(
            JellyfinMovie movie,
            int startPositionMs = 0,
            int? subtitleStreamIndex = null,
            bool burnIn = false,
            string preferredMediaSourceId = null
        )
        {
            _movie = movie;
            _startPositionMs = startPositionMs;
            _initialSubtitleIndex = subtitleStreamIndex;
            _burnIn = burnIn;
            _preferredMediaSourceId = preferredMediaSourceId;

            // CRITICAL: Ensure the window is transparent so the video plane is visible.
            Window.Default.BackgroundColor = Color.Transparent;
            BackgroundColor = Color.Transparent;
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
                _subtitleEnabled = _initialSubtitleIndex.HasValue;
                _subtitleOffsetBurnInWarningShown = false;
                _useParsedSubtitleRenderer = false;
                _subtitleCues.Clear();
                _activeSubtitleCueIndex = -1;
                _player = new Player();

                _player.ErrorOccurred += (s, e) => { };
                _player.BufferingProgressChanged += (s, e) => { };
                _player.PlaybackCompleted += OnPlaybackCompleted;
                _player.SubtitleUpdated += OnSubtitleUpdated;

                _player.Display = new Display(Window.Default);
                _player.DisplaySettings.Mode = PlayerDisplayMode.LetterBox;
                _player.DisplaySettings.IsVisible = true;

                var playbackInfo = await AppState.Jellyfin.GetPlaybackInfoAsync(_movie.Id, _initialSubtitleIndex, _burnIn);
                if (playbackInfo.MediaSources == null || playbackInfo.MediaSources.Count == 0)
                    return;

                var mediaSource = playbackInfo.MediaSources[0];
                if (!string.IsNullOrWhiteSpace(_preferredMediaSourceId))
                {
                    var preferredSource = playbackInfo.MediaSources.FirstOrDefault(s => s.Id == _preferredMediaSourceId);
                    if (preferredSource != null)
                        mediaSource = preferredSource;
                }
                _currentMediaSource = mediaSource; 
                _playSessionId = playbackInfo.PlaySessionId;

                try
                {
                    _isAnamorphicVideo = await AppState.Jellyfin.GetIsAnamorphicAsync(_movie.Id);
                }
                catch
                {
                    _isAnamorphicVideo = false;
                }

                ApplyDisplayModeForCurrentVideo();

                string streamUrl = "";
                var apiKey = AppState.AccessToken;
                var serverUrl = AppState.Jellyfin.ServerUrl;
                bool supportsDirectPlay = mediaSource.SupportsDirectPlay;
                bool supportsTranscoding = mediaSource.SupportsTranscoding;
                bool hasTranscodeUrl = !string.IsNullOrEmpty(mediaSource.TranscodingUrl);
                bool forceTranscode = _overrideAudioIndex.HasValue || (_burnIn && _initialSubtitleIndex.HasValue);

                if (supportsDirectPlay && (!forceTranscode || !supportsTranscoding))
                {
                    _playMethod = "DirectPlay";
                    streamUrl = $"{serverUrl}/Videos/{_movie.Id}/stream?static=true&MediaSourceId={mediaSource.Id}&PlaySessionId={_playSessionId}&api_key={apiKey}";
                }
                else if (supportsTranscoding || forceTranscode)
                {
                    _playMethod = "Transcode";

                    var videoStream = mediaSource.MediaStreams.FirstOrDefault(s => s.Type == "Video");
                    string vidCodec = videoStream?.Codec?.ToLower() ?? "unknown";
                    bool isVideoNative = vidCodec.Contains("h264") || vidCodec.Contains("hevc") || 
                                         vidCodec.Contains("vp9") || vidCodec.Contains("av1");
                    string container = (isVideoNative && !_burnIn) ? "mp4" : "ts";
                    string audioPriority = "ac3,eac3,aac,mp3";

                    if (!hasTranscodeUrl)
                    {
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
                    if (_overrideAudioIndex.HasValue)
                        streamUrl = AppendParam(streamUrl, $"AudioStreamIndex={_overrideAudioIndex.Value}");

                    if (_initialSubtitleIndex.HasValue)
                    {
                        if (!streamUrl.Contains("SubtitleStreamIndex=")) streamUrl = AppendParam(streamUrl, $"SubtitleStreamIndex={_initialSubtitleIndex}");
                        if (_burnIn && !streamUrl.Contains("SubtitleMethod=")) streamUrl = AppendParam(streamUrl, "SubtitleMethod=Encode");
                    }

                    streamUrl = streamUrl.Replace("?&", "?").Replace("&&", "&").Replace(" ", "%20").Replace("\n", "").Replace("\r", "");
                }
                else
                    return;

                if (!_burnIn)
                {
                    var externalSubStreams = mediaSource.MediaStreams?.Where(s => s.Type == "Subtitle" && s.IsExternal).ToList();
                    if (externalSubStreams != null && externalSubStreams.Count > 0)
                    {
                        var subStream = externalSubStreams.First();
                        _externalSubtitleIndex = subStream.Index;
                        _externalSubtitleMediaSourceId = mediaSource.Id;
                        _externalSubtitleCodec = subStream.Codec;
                        _externalSubtitleLanguage = !string.IsNullOrEmpty(subStream.Language) ? subStream.Language.ToUpper() : "EXTERNAL";
                    }
                    else
                    {
                        var internalSubStreams = mediaSource.MediaStreams?.Where(s => s.Type == "Subtitle" && !s.IsExternal).ToList();
                        if (internalSubStreams != null && internalSubStreams.Count > 0)
                        {
                            if (_playMethod == "Transcode")
                            {
                                var subStream = internalSubStreams.First();
                                _externalSubtitleIndex = subStream.Index;
                                _externalSubtitleMediaSourceId = mediaSource.Id;
                                _externalSubtitleCodec = subStream.Codec;
                                _externalSubtitleLanguage = !string.IsNullOrEmpty(subStream.Language) ? subStream.Language.ToUpper() : "EXTERNAL";
                            }
                        }
                    }

                    if (_initialSubtitleIndex.HasValue)
                    {
                        var subStream = mediaSource.MediaStreams?.FirstOrDefault(s => s.Type == "Subtitle" && s.Index == _initialSubtitleIndex.Value);
                        if (subStream != null)
                        {
                            _externalSubtitleIndex = _initialSubtitleIndex.Value;
                            _externalSubtitleMediaSourceId = mediaSource.Id;
                            _externalSubtitleCodec = subStream.Codec;
                            _externalSubtitleLanguage = !string.IsNullOrEmpty(subStream.Language) ? subStream.Language.ToUpper() : "EXTERNAL";
                        }
                    }
                }

                var source = new MediaUriSource(streamUrl);
                _player.SetSource(source);

                if (!_burnIn && _initialSubtitleIndex.HasValue)
                {
                    try 
                    {
                        var subStream = mediaSource.MediaStreams?.FirstOrDefault(s => s.Type == "Subtitle" && s.Index == _initialSubtitleIndex.Value);
                        if (subStream != null)
                            await DownloadAndSetSubtitle(mediaSource.Id, _initialSubtitleIndex.Value, subStream.Codec);
                    }
                    catch { }
                }

                await _player.PrepareAsync();
                ApplyDisplayModeForCurrentVideo();

                try { _ = _player.StreamInfo.GetVideoProperties(); } catch { }
                try { _ = _player.AudioTrackInfo.GetCount(); } catch { }
                try 
                {
                    int subCount = _player.SubtitleTrackInfo.GetCount();
                    if (_initialSubtitleIndex.HasValue && subCount > 0 && string.IsNullOrEmpty(_externalSubtitlePath))
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
                                _player.SubtitleTrackInfo.Selected = tizenIndex;
                                _subtitleIndex = tizenIndex + 1;
                                _subtitleEnabled = true;
                            }
                        }
                    }
                } catch {}

                if (_useParsedSubtitleRenderer) TryDisableNativeSubtitleTrack();

                if (_startPositionMs > 0)
                    await _player.SetPlayPositionAsync(_startPositionMs, false);

                _player.Start();
                ApplyDisplayModeForCurrentVideo();
                ApplySubtitleOffset();

                var info = new PlaybackProgressInfo
                {
                    ItemId = _movie.Id, PlaySessionId = _playSessionId, MediaSourceId = mediaSource.Id,
                    PositionTicks = _startPositionMs * 10000, IsPaused = false, PlayMethod = _playMethod, EventName = "TimeUpdate"
                };
                _ = AppState.Jellyfin.ReportPlaybackStartAsync(info);
            }
            catch { }
        }

        private async Task<bool> DownloadAndSetSubtitle(string mediaSourceId, int subtitleIndex, string _codec)
        {
            try
            {
                string ext = "srt";

                var apiKey = AppState.AccessToken;
                var serverUrl = AppState.Jellyfin.ServerUrl;

                var downloadUrl = $"{serverUrl}/Videos/{_movie.Id}/{mediaSourceId}/Subtitles/{subtitleIndex}/0/Stream.{ext}?api_key={apiKey}";
                var localPath = System.IO.Path.Combine(Application.Current.DirectoryInfo.Data, $"sub_{mediaSourceId}_{subtitleIndex}.{ext}");

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("X-Emby-Authorization", $"MediaBrowser Client=\"JellyfinTizen\", Device=\"SamsungTV\", DeviceId=\"tizen-tv\", Version=\"1.0\", Token=\"{apiKey}\"");
                    var response = await client.GetAsync(downloadUrl);
                    response.EnsureSuccessStatusCode();
                    var data = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(localPath, data);
                }
                _externalSubtitlePath = localPath;

                if (TryLoadSrtSubtitleCues(localPath))
                {
                    _useParsedSubtitleRenderer = true;
                    TryDisableNativeSubtitleTrack();
                    StartSubtitleRenderTimer();
                }
                else
                {
                    _useParsedSubtitleRenderer = false;
                    _player.SetSubtitle(localPath);
                }

                return true;
            }
            catch
            {
                _externalSubtitlePath = null;
                _useParsedSubtitleRenderer = false;
                _subtitleCues.Clear();
                StopSubtitleRenderTimer();
                return false;
            }
        }

        private bool TryLoadSrtSubtitleCues(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;

                string raw = File.ReadAllText(path);
                string normalized = raw.Replace("\r\n", "\n").Replace('\r', '\n');
                string[] blocks = normalized.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
                var cues = new List<SubtitleCue>();

                foreach (string block in blocks)
                {
                    var lines = block.Split('\n')
                        .Select(l => l.TrimEnd())
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .ToList();

                    if (lines.Count < 2) continue;

                    int timeLineIndex = lines[0].Contains("-->") ? 0 : 1;
                    if (timeLineIndex >= lines.Count) continue;

                    string timeLine = lines[timeLineIndex];
                    string[] parts = timeLine.Split(new[] { "-->" }, StringSplitOptions.None);
                    if (parts.Length != 2) continue;

                    if (!TryParseSubtitleTimestamp(parts[0], out int startMs)) continue;
                    if (!TryParseSubtitleTimestamp(parts[1], out int endMs)) continue;

                    if (endMs <= startMs) endMs = startMs + 500;

                    string cueText = string.Join("\n", lines.Skip(timeLineIndex + 1));
                    if (string.IsNullOrWhiteSpace(cueText)) continue;

                    cues.Add(new SubtitleCue
                    {
                        StartMs = startMs,
                        EndMs = endMs,
                        Text = cueText
                    });
                }

                cues.Sort((a, b) => a.StartMs.CompareTo(b.StartMs));
                _subtitleCues = cues;
                _activeSubtitleCueIndex = -1;
                return cues.Count > 0;
            }
            catch
            {
                _subtitleCues.Clear();
                _activeSubtitleCueIndex = -1;
                return false;
            }
        }

        private bool TryParseSubtitleTimestamp(string input, out int totalMs)
        {
            totalMs = 0;
            if (string.IsNullOrWhiteSpace(input)) return false;

            string token = input.Trim();
            int spaceIndex = token.IndexOf(' ');
            if (spaceIndex >= 0) token = token.Substring(0, spaceIndex);

            token = token.Replace('.', ',');
            string[] parts = token.Split(':');
            if (parts.Length != 3) return false;

            if (!int.TryParse(parts[0], out int hours)) return false;
            if (!int.TryParse(parts[1], out int minutes)) return false;

            string[] secParts = parts[2].Split(',');
            if (secParts.Length != 2) return false;
            if (!int.TryParse(secParts[0], out int seconds)) return false;

            string msPart = secParts[1].Trim();
            if (msPart.Length > 3) msPart = msPart.Substring(0, 3);
            while (msPart.Length < 3) msPart += "0";
            if (!int.TryParse(msPart, out int millis)) return false;

            totalMs = (((hours * 60) + minutes) * 60 + seconds) * 1000 + millis;
            return true;
        }

        private void StartSubtitleRenderTimer()
        {
            StopSubtitleRenderTimer();
            _subtitleHideTimer?.Stop();
            _activeSubtitleCueIndex = -1;
            _subtitleRenderTimer = new Timer(80);
            _subtitleRenderTimer.Tick += OnSubtitleRenderTick;
            _subtitleRenderTimer.Start();
            UpdateParsedSubtitleRender();
        }

        private void StopSubtitleRenderTimer()
        {
            try
            {
                _subtitleRenderTimer?.Stop();
                _subtitleRenderTimer = null;
            }
            catch { }
        }

        private bool OnSubtitleRenderTick(object sender, Timer.TickEventArgs e)
        {
            UpdateParsedSubtitleRender();
            return true;
        }

        private void UpdateParsedSubtitleRender()
        {
            if (!_useParsedSubtitleRenderer || _subtitleText == null || _player == null || !_subtitleEnabled || _subtitleCues.Count == 0)
            {
                _subtitleText?.Hide();
                _activeSubtitleCueIndex = -1;
                return;
            }

            int queryPosMs = GetPlayPositionMs() - _subtitleOffsetMs;
            if (queryPosMs < 0)
            {
                _subtitleText.Hide();
                _activeSubtitleCueIndex = -1;
                return;
            }

            int cueIndex = -1;
            for (int i = 0; i < _subtitleCues.Count; i++)
            {
                var cue = _subtitleCues[i];
                if (queryPosMs < cue.StartMs) break;
                if (queryPosMs <= cue.EndMs)
                {
                    cueIndex = i;
                    break;
                }
            }

            if (cueIndex == -1)
            {
                if (_activeSubtitleCueIndex != -1) _subtitleText.Hide();
                _activeSubtitleCueIndex = -1;
                return;
            }

            if (cueIndex != _activeSubtitleCueIndex)
            {
                _activeSubtitleCueIndex = cueIndex;
                _subtitleText.Text = NormalizeParsedCueText(_subtitleCues[cueIndex].Text);
                _subtitleText.Show();

            }
        }

        private string NormalizeParsedCueText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            string normalized = text.Replace("\r", "").Replace("\\N", "\n").Replace("\\h", " ");
            normalized = Regex.Replace(normalized, "(?i)<br\\s*/?>", "\n");
            // Remove common ASS/SSA inline override tags.
            normalized = Regex.Replace(normalized, "\\{\\\\[^\\}]*\\}", string.Empty);
            // Strip HTML-like formatting tags from SRT payloads.
            normalized = Regex.Replace(normalized, "<[^>]+>", string.Empty);
            normalized = WebUtility.HtmlDecode(normalized);
            return normalized.Trim();
        }

        private void TryDisableNativeSubtitleTrack()
        {
            if (_player == null) return;
            try
            {
                if (_player.SubtitleTrackInfo.GetCount() > 0) _player.SubtitleTrackInfo.Selected = -1;
            }
            catch { }
        }

        private void ApplyDisplayModeForCurrentVideo()
        {
            if (_player == null)
                return;

            _player.DisplaySettings.Mode = ResolveDisplayMode(_isAnamorphicVideo);
        }

        private static PlayerDisplayMode ResolveDisplayMode(bool isAnamorphic)
        {
            if (!isAnamorphic)
                return PlayerDisplayMode.LetterBox;

            // Prefer fullscreen-like modes for anamorphic content.
            if (TryParseDisplayMode("FullScreen", out var mode)) return mode;
            if (TryParseDisplayMode("CroppedFull", out mode)) return mode;
            if (TryParseDisplayMode("CropFull", out mode)) return mode;
            if (TryParseDisplayMode("OriginalSize", out mode)) return mode;
            if (TryParseDisplayMode("Original", out mode)) return mode;

            return PlayerDisplayMode.LetterBox;
        }

        private static bool TryParseDisplayMode(string name, out PlayerDisplayMode mode)
        {
            try
            {
                mode = (PlayerDisplayMode)Enum.Parse(typeof(PlayerDisplayMode), name, ignoreCase: true);
                return true;
            }
            catch
            {
                mode = PlayerDisplayMode.LetterBox;
                return false;
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

            _topOsd.Add(CreateTopOsdTitleView(sidePadding));
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

            var progressRow = new View { WidthResizePolicy = ResizePolicyType.FillToParent, HeightSpecification = 50, PositionY = 90 };
            _currentTimeLabel = new TextLabel("00:00") { PositionX = sidePadding, WidthSpecification = labelWidth, HeightResizePolicy = ResizePolicyType.FillToParent, PointSize = 26, TextColor = Color.White, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Begin };
            _durationLabel = new TextLabel("00:00") { PositionX = screenWidth - sidePadding - labelWidth, WidthSpecification = labelWidth, HeightResizePolicy = ResizePolicyType.FillToParent, PointSize = 26, TextColor = Color.White, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.End };

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

            _controlsContainer = new View { WidthResizePolicy = ResizePolicyType.FillToParent, HeightSpecification = 80, PositionY = 155, Layout = new LinearLayout { LinearOrientation = LinearLayout.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, CellPadding = new Size2D(40, 0) } };
            _prevButton = CreateOsdButton("Prev");
            _nextButton = CreateOsdButton("Next");

            _osdButtonCount = 0;
            if (_movie.ItemType == "Episode")
            {
                _controlsContainer.Add(_prevButton);
                _controlsContainer.Add(_nextButton);
                _osdButtonCount = 2;
                _buttonFocusIndex = 0; // 0 = Prev, 1 = Next
            }

            CreateSubtitleOffsetTrack(screenWidth);
            UpdateSubtitleOffsetUI();

            _osd.Add(progressRow);
            _osd.Add(_subtitleOffsetTrackContainer);
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

        private View CreateTopOsdTitleView(int sidePadding)
        {
            // Keep textual title for episodes in OSD.
            if (_movie.ItemType == "Episode")
                return CreateTopOsdTitleLabel(sidePadding, GetFallbackOsdTitleText());

            if (_movie.HasLogo)
            {
                var logoUrl = AppState.GetItemLogoUrl(_movie.Id, 720);
                if (!string.IsNullOrWhiteSpace(logoUrl))
                {
                    int topLogoWidth = 340;
                    var logoContainer = new View
                    {
                        PositionX = sidePadding,
                        PositionY = 24,
                        WidthSpecification = topLogoWidth,
                        HeightSpecification = 116,
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
            }

            return CreateTopOsdTitleLabel(sidePadding, GetFallbackOsdTitleText());
        }

        private TextLabel CreateTopOsdTitleLabel(int sidePadding, string text)
        {
            return new TextLabel(text)
            {
                PositionX = sidePadding,
                PositionY = 40,
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                PointSize = 36,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Begin,
                VerticalAlignment = VerticalAlignment.Top
            };
        }

        private string GetFallbackOsdTitleText()
        {
            return _movie.ItemType == "Episode"
                ? $"{_movie.SeriesName} S{_movie.ParentIndexNumber}:E{_movie.IndexNumber} - {_movie.Name}"
                : _movie.Name;
        }

        private View CreateOsdButton(string text)
        {
            var btn = new View { WidthSpecification = 150, HeightSpecification = 60, BackgroundColor = new Color(1, 1, 1, 0.15f), CornerRadius = 30.0f };
            var label = new TextLabel(text) { WidthResizePolicy = ResizePolicyType.FillToParent, HeightResizePolicy = ResizePolicyType.FillToParent, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextColor = Color.White, PointSize = 24 };
            btn.Add(label);
            return btn;
        }

        private void SetButtonVisual(View button, bool focused, bool editing = false)
        {
             if (button == null) return;
             button.BackgroundColor = focused
                 ? (editing ? new Color(0, 164f / 255f, 220f / 255f, 1f) : new Color(0.85f, 0.11f, 0.11f, 1f))
                 : new Color(1, 1, 1, 0.15f);
             button.Scale = focused ? new Vector3(1.1f, 1.1f, 1f) : Vector3.One;
        }

        private void CreateSubtitleOffsetTrack(int screenWidth)
        {
            int trackX = (screenWidth - SubtitleOffsetTrackWidth) / 2;
            _subtitleOffsetTrackContainer = new View
            {
                WidthSpecification = SubtitleOffsetTrackWidth,
                HeightSpecification = 36,
                PositionX = trackX,
                PositionY = 138
            };

            var trackLine = new View
            {
                WidthSpecification = SubtitleOffsetTrackWidth,
                HeightSpecification = 2,
                PositionY = 17,
                BackgroundColor = new Color(1, 1, 1, 0.45f),
                CornerRadius = 1.0f
            };

            _subtitleOffsetCenterMarker = new View
            {
                WidthSpecification = 2,
                HeightSpecification = 16,
                PositionX = (SubtitleOffsetTrackWidth / 2) - 1,
                PositionY = 10,
                BackgroundColor = new Color(1, 1, 1, 0.85f)
            };

            _subtitleOffsetThumb = new View
            {
                WidthSpecification = 12,
                HeightSpecification = 12,
                PositionY = 12,
                BackgroundColor = new Color(0, 164f / 255f, 220f / 255f, 1f),
                CornerRadius = 6.0f
            };

            _subtitleOffsetTrackContainer.Add(trackLine);
            _subtitleOffsetTrackContainer.Add(_subtitleOffsetCenterMarker);
            _subtitleOffsetTrackContainer.Add(_subtitleOffsetThumb);
            _subtitleOffsetTrackContainer.Hide();
        }

        private void SetOsdButtonText(View button, string text)
        {
            if (button == null || button.ChildCount == 0) return;
            if (button.GetChildAt(0) is TextLabel label) label.Text = text;
        }

        private string FormatSubtitleOffsetLabel()
        {
            float seconds = _subtitleOffsetMs / 1000f;
            string sign = seconds > 0 ? "+" : "";
            return $"{sign}{seconds:0.0}s";
        }

        private void UpdateSubtitleOffsetUI()
        {
            if (_subtitleOffsetButton != null && _subtitleOffsetButton.ChildCount > 0)
            {
                if (_subtitleOffsetButton.GetChildAt(0) is TextLabel label)
                {
                    label.Text = $"Offset: {FormatSubtitleOffsetLabel()}";
                }
            }
            UpdateSubtitleOffsetTrackThumb();
        }

        private void UpdateSubtitleOffsetTrackThumb()
        {
            if (_subtitleOffsetThumb == null) return;
            float ratio = (float)(_subtitleOffsetMs + SubtitleOffsetLimitMs) / (SubtitleOffsetLimitMs * 2);
            ratio = Math.Clamp(ratio, 0f, 1f);
            int usableWidth = SubtitleOffsetTrackWidth - _subtitleOffsetThumb.WidthSpecification;
            _subtitleOffsetThumb.PositionX = (int)Math.Round(usableWidth * ratio);
        }

        private void AdjustSubtitleOffset(int deltaMs)
        {
            int newOffset = Math.Clamp(_subtitleOffsetMs + deltaMs, -SubtitleOffsetLimitMs, SubtitleOffsetLimitMs);
            if (newOffset == _subtitleOffsetMs) return;

            _subtitleOffsetMs = newOffset;
            ApplySubtitleOffset();
            UpdateSubtitleOffsetUI();
            _osdTimer.Stop();
            _osdTimer.Start();}

        private void ApplySubtitleOffset()
        {
            if (_player == null) return;
            if (_useParsedSubtitleRenderer)
            {
                _activeSubtitleCueIndex = -1;
                UpdateParsedSubtitleRender();
                return;
            }

            if (!_subtitleEnabled) return;
            if (_burnIn)
            {
                if (!_subtitleOffsetBurnInWarningShown)
                {_subtitleOffsetBurnInWarningShown = true;
                }
                return;
            }

            try
            {
                _player.SetSubtitleOffset(_subtitleOffsetMs);
            }
            catch (Exception ex)
            {}
        }

        private void ToggleSubtitleOffsetAdjustMode()
        {
            _subtitleOffsetAdjustMode = !_subtitleOffsetAdjustMode;
            if (_subtitleOffsetTrackContainer != null)
            {
                if (_subtitleOffsetAdjustMode) _subtitleOffsetTrackContainer.Show();
                else _subtitleOffsetTrackContainer.Hide();
            }
            // Update visual for subtitle offset button
            if (_subtitleOffsetButton != null)
            {
                _subtitleOffsetButton.BackgroundColor = _subtitleOffsetAdjustMode 
                    ? new Color(0, 164f / 255f, 220f / 255f, 1f) // Blue when editing
                    : new Color(1, 1, 1, 0.15f); // Default transparent
                
                _subtitleOffsetButton.Scale = _subtitleOffsetAdjustMode ? new Vector3(1.05f, 1.05f, 1f) : Vector3.One;
            }
            _osdTimer.Stop();
            _osdTimer.Start();
        }

        private void ExitSubtitleOffsetAdjustMode()
        {
            if (!_subtitleOffsetAdjustMode) return;
            _subtitleOffsetAdjustMode = false;
            _subtitleOffsetTrackContainer?.Hide();
            // Reset subtitle offset button visual
            if (_subtitleOffsetButton != null)
            {
                _subtitleOffsetButton.BackgroundColor = new Color(1, 1, 1, 0.15f);
                _subtitleOffsetButton.Scale = Vector3.One;
            }
            UpdateOsdFocus();
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
            _subtitleOverlay = new View { WidthSpecification = 450, HeightSpecification = 500, BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.95f), PositionX = Window.Default.Size.Width - 500, PositionY = Window.Default.Size.Height - 780, CornerRadius = 16.0f, ClippingMode = ClippingModeType.ClipChildren };
            _subtitleOverlay.Hide();
            Add(_subtitleOverlay);
        }

        private void CreateSubtitleText()
        {
            _subtitleText = new TextLabel("") { WidthResizePolicy = ResizePolicyType.FillToParent, HeightSpecification = 200, PointSize = 46, TextColor = Color.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, BackgroundColor = Color.Transparent, PositionY = Window.Default.Size.Height - 250, MultiLine = true, LineWrapMode = LineWrapMode.Word, Padding = new Extents(150, 150, 0, 0), EnableMarkup = false };
            _subtitleText.Hide();
            Add(_subtitleText);
        }

        private void ShowSubtitleOverlay()
        {
            if (_player == null) return;
            if (_subtitleOverlay == null) CreateSubtitleOverlay();
            HideAudioOverlay();
            ExitSubtitleOffsetAdjustMode();

            // 1. Use Jellyfin Metadata to list ALL tracks (fixes Tizen HLS limitation)
            var subtitleStreams = _currentMediaSource?.MediaStreams?
                .Where(s => s.Type == "Subtitle")
                .OrderBy(s => s.Index)
                .ToList();

            if (_subtitleOverlay.ChildCount == 0)
            {
                _subtitleOverlay.Add(new TextLabel("Subtitles") { WidthResizePolicy = ResizePolicyType.FillToParent, HeightSpecification = 80, PointSize = 34, TextColor = Color.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
                
                // Subtitle Offset Button and Track - Minimal Design
                var offsetContainer = new View { WidthResizePolicy = ResizePolicyType.FillToParent, HeightSpecification = 80, PositionY = 80 };
                _subtitleOffsetButton = new View { WidthSpecification = 280, HeightSpecification = 45, BackgroundColor = new Color(1, 1, 1, 0.15f), CornerRadius = 22.5f, PositionX = 85 };
                var offsetLabel = new TextLabel($"Offset: {FormatSubtitleOffsetLabel()}") { WidthResizePolicy = ResizePolicyType.FillToParent, HeightResizePolicy = ResizePolicyType.FillToParent, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextColor = Color.White, PointSize = 20 };
                _subtitleOffsetButton.Add(offsetLabel);
                offsetContainer.Add(_subtitleOffsetButton);
                
                CreateSubtitleOffsetTrack(280); // Smaller track width
                _subtitleOffsetTrackContainer.PositionX = 85;
                _subtitleOffsetTrackContainer.PositionY = 40;
                offsetContainer.Add(_subtitleOffsetTrackContainer);
                
                _subtitleOverlay.Add(offsetContainer);
                
                _subtitleScrollView = new ScrollableBase 
                { 
                    WidthSpecification = 410, 
                    HeightSpecification = 320, 
                    PositionX = 20, 
                    PositionY = 160, 
                    ScrollingDirection = ScrollableBase.Direction.Vertical,
                    BackgroundColor = Color.Transparent
                };
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
            var row = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent, HeightSpecification = 60,
                BackgroundColor = Color.Transparent, CornerRadius = 8.0f,
                Margin = new Extents(20, 20, 5, 5),
                Focusable = true
            };
            row.Name = indexId; 
            
            row.FocusGained += (s, e) => {
                // When Tizen moves focus here, sync our internal index and visuals
                if (_subtitleListContainer != null)
                {
                    // Find index of this row
                    for (int i = 0; i < _subtitleListContainer.ChildCount; i++)
                    {
                        if (_subtitleListContainer.GetChildAt((uint)i) == row)
                        {
                            _subtitleIndex = i;
                            UpdateSubtitleVisuals(false); // Update colors only
                            break;
                        }
                    }
                }
            };

            row.FocusLost += (s, e) =>
            {
                row.BackgroundColor = Color.Transparent;
                var label = row.GetChildAt(0) as TextLabel;
                if (label != null) label.TextColor = new Color(1, 1, 1, 0.6f);
                row.Scale = Vector3.One;
            };
            
            var label = new TextLabel(text) { WidthResizePolicy = ResizePolicyType.FillToParent, HeightResizePolicy = ResizePolicyType.FillToParent, PointSize = 28, TextColor = new Color(1, 1, 1, 0.6f), HorizontalAlignment = HorizontalAlignment.Begin, VerticalAlignment = VerticalAlignment.Center, Padding = new Extents(20, 0, 0, 0) };
            row.Add(label);
            return row;
        }

        private void UpdateSubtitleVisuals(bool setFocus = true)
        {
            if (_subtitleListContainer == null) return;
            int count = (int)_subtitleListContainer.ChildCount;
            View focusedView = null;
            for (int i = 0; i < count; i++)
            {
                var row = _subtitleListContainer.GetChildAt((uint)i);
                bool selected = (i == _subtitleIndex);
                row.BackgroundColor = selected ? new Color(1, 1, 1, 0.25f) : Color.Transparent;
                row.Scale = selected ? new Vector3(1.05f, 1.05f, 1.0f) : Vector3.One;
                var label = row.GetChildAt(0) as TextLabel;
                if (label != null)
                {
                    label.TextColor = selected ? Color.White : new Color(1, 1, 1, 0.6f);
                }
                if (selected)
                {
                    focusedView = row;
                }
            }
            if (setFocus && focusedView != null) FocusManager.Instance.SetCurrentFocusView(focusedView);
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
                _useParsedSubtitleRenderer = false;
                _subtitleCues.Clear();
                _activeSubtitleCueIndex = -1;
                StopSubtitleRenderTimer();
                try { _player?.ClearSubtitle(); } catch { }
                RunOnUiThread(() =>
                {
                    _subtitleHideTimer?.Stop();
                    _subtitleText?.Hide();
                });
                return;
            }

            if (!int.TryParse(selectedName, out int jellyfinStreamIndex)) return;
            _subtitleEnabled = true;

            var subStream = _currentMediaSource?.MediaStreams?.FirstOrDefault(s => s.Index == jellyfinStreamIndex);
            if (subStream == null) return;

            bool sidecarSet = false;
            if (!_burnIn)
            {
                sidecarSet = await DownloadAndSetSubtitle(_currentMediaSource.Id, jellyfinStreamIndex, subStream.Codec);
                if (sidecarSet)
                {
                    ApplySubtitleOffset();
                    return;
                }
            }

            // Fallback: native internal switch if sidecar extraction is unavailable.
            bool tryNative = _playMethod == "DirectPlay" && !subStream.IsExternal;
            if (tryNative)
            {
                try
                {
                    var embeddedSubs = _currentMediaSource.MediaStreams.Where(s => s.Type == "Subtitle" && !s.IsExternal).OrderBy(s => s.Index).ToList();
                    int tizenIndex = embeddedSubs.FindIndex(s => s.Index == jellyfinStreamIndex);
                    if (tizenIndex != -1 && tizenIndex < _player.SubtitleTrackInfo.GetCount())
                    {_useParsedSubtitleRenderer = false;
                        _subtitleCues.Clear();
                        _activeSubtitleCueIndex = -1;
                        StopSubtitleRenderTimer();
                        _subtitleText?.Hide();
                        _player.SubtitleTrackInfo.Selected = tizenIndex;
                        ApplySubtitleOffset();
                        return;
                    }
                }
                catch {}
            }}

        private void CreateAudioOverlay()
        {
            _audioOverlay = new View { WidthSpecification = 450, HeightSpecification = 500, BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.95f), PositionX = Window.Default.Size.Width - 500, PositionY = Window.Default.Size.Height - 780, CornerRadius = 16.0f, ClippingMode = ClippingModeType.ClipChildren };
            _audioOverlay.Hide();
            Add(_audioOverlay);
        }

        private void ShowAudioOverlay()
        {
            if (_player == null) return;
            if (_audioOverlay == null) CreateAudioOverlay();
            HideSubtitleOverlay();
            ExitSubtitleOffsetAdjustMode();

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

                    var row = new View { WidthResizePolicy = ResizePolicyType.FillToParent, HeightSpecification = 60, BackgroundColor = Color.Transparent, CornerRadius = 8.0f, Margin = new Extents(20, 20, 5, 5), Focusable = true };
                    row.Name = stream.Index.ToString();
                    
                    // Handle focus events to remove default blue border
                    row.FocusGained += (s, e) => {
                        // When Tizen moves focus here, sync our internal index and visuals
                        if (_audioListContainer != null)
                        {
                            for (int i = 0; i < _audioListContainer.ChildCount; i++)
                            {
                                if (_audioListContainer.GetChildAt((uint)i) == row)
                                {
                                    _audioIndex = i;
                                    UpdateAudioVisuals(false); // Update colors only
                                    break;
                                }
                            }
                        }
                    };

                    row.FocusLost += (s, e) =>
                    {
                        row.BackgroundColor = Color.Transparent;
                        var label = row.GetChildAt(0) as TextLabel;
                        if (label != null) label.TextColor = new Color(1, 1, 1, 0.6f);
                        row.Scale = Vector3.One;
                    };
                    var label = new TextLabel(displayText) { WidthResizePolicy = ResizePolicyType.FillToParent, HeightResizePolicy = ResizePolicyType.FillToParent, PointSize = 26, TextColor = new Color(1, 1, 1, 0.6f), HorizontalAlignment = HorizontalAlignment.Begin, VerticalAlignment = VerticalAlignment.Center, Padding = new Extents(20, 0, 0, 0) };
                    row.Add(label);
                    _audioListContainer.Add(row);
                }
            }
            UpdateAudioVisuals();
            _audioOverlay.Show();
            _audioOverlayVisible = true;
        }

        private void UpdateAudioVisuals(bool setFocus = true)
        {
            if (_audioListContainer == null) return;
            int count = (int)_audioListContainer.ChildCount;
            View focusedView = null;
            for (int i = 0; i < count; i++)
            {
                var row = _audioListContainer.GetChildAt((uint)i);
                bool selected = (i == _audioIndex);
                row.BackgroundColor = selected ? new Color(1, 1, 1, 0.25f) : Color.Transparent;
                row.Scale = selected ? new Vector3(1.05f, 1.05f, 1.0f) : Vector3.One;
                var label = row.GetChildAt(0) as TextLabel;
                if (label != null) label.TextColor = selected ? Color.White : new Color(1, 1, 1, 0.6f);
            }
            if (setFocus && focusedView != null) FocusManager.Instance.SetCurrentFocusView(focusedView);
        }

        private void HideAudioOverlay() { if (_audioOverlay != null) { _audioOverlay.Hide(); _audioOverlayVisible = false; } }
        private void HideSubtitleOverlay() { if (_subtitleOverlay != null) { _subtitleOverlay.Hide(); _subtitleOverlayVisible = false; } }

        private void OnSubtitleUpdated(object sender, SubtitleUpdatedEventArgs e)
        {
            if (_useParsedSubtitleRenderer) return;

            RunOnUiThread(() =>
            {
                string text = e.Text;
                _subtitleHideTimer?.Stop();

                if (_subtitleText == null || !_subtitleEnabled || string.IsNullOrWhiteSpace(text))
                {
                    _subtitleText?.Hide();
                    return;
                }

                _subtitleText.Text = text;
                _subtitleText.Show();

                uint hideDurationMs = (uint)Math.Clamp((long)e.Duration, 200L, uint.MaxValue);
                _subtitleHideTimer = new Timer(hideDurationMs);
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

        private void SelectAudioTrack() 
        { 
            if (_audioListContainer == null) return; 
            if (_audioIndex < 0 || _audioIndex >= _audioListContainer.ChildCount) return;
            var selectedRow = _audioListContainer.GetChildAt((uint)_audioIndex);
            
            if (!int.TryParse(selectedRow.Name, out int jellyfinStreamIndex)) return;

            HideAudioOverlay();
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
                               (s.Codec != null && (s.Codec.ToLower().Contains("aac") || s.Codec.ToLower().Contains("ac3") || 
                                s.Codec.ToLower().Contains("eac3") || s.Codec.ToLower().Contains("mp3"))))
                        .OrderBy(s => s.Index)
                        .ToList();

                    int tizenIndex = supportedTracks.FindIndex(s => s.Index == jellyfinStreamIndex);
                    if (tizenIndex != -1 && tizenIndex < _player.AudioTrackInfo.GetCount())
                    {
                        _player.AudioTrackInfo.Selected = tizenIndex;
                        return;
                    }
                }
                catch {}
            }

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
            if (_subtitleOffsetTrackContainer != null)
            {
                if (_subtitleOffsetAdjustMode) _subtitleOffsetTrackContainer.Show();
                else _subtitleOffsetTrackContainer.Hide();
            }
            _osdVisible = true;
            if (_subtitleText != null) _subtitleText.PositionY = Window.Default.Size.Height - 340;
            UpdateOsdFocus();
            UpdateSubtitleOffsetUI();
            UpdateProgress();
            _osdTimer.Stop(); _osdTimer.Start();
            _progressTimer.Start();
        }

        private void UpdateOsdFocus()
        {
            if (_progressTrack == null) return;

            if (_osdFocusRow == 0)
            {
                _progressTrack.BackgroundColor = new Color(1, 1, 1, 0.4f);
                SetButtonVisual(_prevButton, false);
                SetButtonVisual(_nextButton, false);
            }
            else
            {
                _progressTrack.BackgroundColor = new Color(1, 1, 1, 0.25f);
                SetButtonVisual(_prevButton, _buttonFocusIndex == 0);
                SetButtonVisual(_nextButton, _buttonFocusIndex == 1);
            }

            if (_subtitleOffsetCenterMarker != null)
            {
                _subtitleOffsetCenterMarker.BackgroundColor = _subtitleOffsetAdjustMode
                    ? new Color(0, 164f / 255f, 220f / 255f, 1f)
                    : new Color(1, 1, 1, 0.85f);
            }
        }

        private void HideOSD()
        {
            ExitSubtitleOffsetAdjustMode();
            _osd.Hide(); _osd.Opacity = 0;
            if (_topOsd != null) { _topOsd.Hide(); _topOsd.Opacity = 0; }
            _osdVisible = false;
            if (_subtitleText != null) _subtitleText.PositionY = Window.Default.Size.Height - 250;
            _osdTimer.Stop();
            _progressTimer.Stop();
            if (_isSeeking && _player != null) { _isSeeking = false; _seekPreviewMs = GetPlayPositionMs(); UpdateProgress(); }
        }

        private bool OnReportProgressTick(object sender, Timer.TickEventArgs e) { ReportProgressToServer(force: false); return true; }

        private void ReportProgressToServer(bool force = false)
        {
            if (_player == null || _isSeeking || _isFinished || _currentMediaSource == null) return;
            if (!force && _player.State != PlayerState.Playing) return;

            var positionMs = GetPlayPositionMs();
            var durationMs = GetDuration();
            if (durationMs <= 0) return;

            var info = new PlaybackProgressInfo
            {
                ItemId = _movie.Id, PlaySessionId = _playSessionId, MediaSourceId = _currentMediaSource.Id,
                PositionTicks = (long)positionMs * 10000, IsPaused = _player.State == PlayerState.Paused,
                PlayMethod = _playMethod, EventName = force ? (_player.State == PlayerState.Paused ? "Pause" : "Unpause") : "TimeUpdate"
            };
            _ = AppState.Jellyfin.ReportPlaybackProgressAsync(info);

            if (((double)positionMs / durationMs) > 0.95 && !_isFinished) { _ = AppState.Jellyfin.MarkAsPlayedAsync(_movie.Id); }
            else { _ = AppState.Jellyfin.UpdatePlaybackPositionAsync(_movie.Id, (long)positionMs * 10000); }
        }

        private void OnPlaybackCompleted(object sender, EventArgs e)
        {_isFinished = true;
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
                try { _subtitleRenderTimer?.Stop(); } catch { }
                _player.SubtitleUpdated -= OnSubtitleUpdated;
                _subtitleHideTimer?.Stop();
                _subtitleText?.Hide();
                _useParsedSubtitleRenderer = false;
                _subtitleCues.Clear();
                _activeSubtitleCueIndex = -1;
                try { _player.Stop(); } catch { }
                try { _player.Unprepare(); } catch { }
                try { _player.Dispose(); } catch { }
                _player = null;
            } catch { }
        }

        public void HandleKey(AppKey key)
        {
            if (key == AppKey.Unknown) return;
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
                    else if (_subtitleOverlayVisible) 
                    {
                        if (_subtitleOffsetAdjustMode) 
                        {
                            ExitSubtitleOffsetAdjustMode();
                        }
                        else 
                        {
                            // Select the currently highlighted subtitle
                            SelectSubtitle();
                        }
                    }
                    else if (_isSeeking) CommitSeek();
                    else if (_osdVisible) { if (_osdFocusRow == 1) ActivateOsdButton(); else { TogglePause(); _osdTimer.Stop(); _osdTimer.Start(); } }
                    else ShowOSD();
                    break;
                case AppKey.Left:
                    if (_audioOverlayVisible)
                    {
                        // Let FocusManager handle audio overlay navigation.
                    }
                    else if (_subtitleOverlayVisible && _subtitleOffsetAdjustMode) AdjustSubtitleOffset(-SubtitleOffsetStepMs);
                    // else if (_subtitleOverlayVisible) MoveSubtitleSelection(-1); // Let FocusManager handle navigation.
                    else if (_osdVisible && _osdFocusRow == 1) MoveButtonFocus(-1);
                    else Scrub(-30);
                    break;
                case AppKey.Right:
                    if (_audioOverlayVisible)
                    {
                        // Let FocusManager handle audio overlay navigation.
                    }
                    else if (_subtitleOverlayVisible && _subtitleOffsetAdjustMode) AdjustSubtitleOffset(SubtitleOffsetStepMs);
                    // else if (_subtitleOverlayVisible) MoveSubtitleSelection(1); // Let FocusManager handle navigation.
                    else if (_osdVisible && _osdFocusRow == 1) MoveButtonFocus(1);
                    else Scrub(30);
                    break;
                case AppKey.Up:
                    if (_audioOverlayVisible)
                    {
                        // Let FocusManager handle audio overlay navigation.
                    }
                    else if (_subtitleOverlayVisible) 
                    {
                        if (_subtitleOffsetAdjustMode) ExitSubtitleOffsetAdjustMode();
                        else 
                        {
                            // Check if we're at the first subtitle, then toggle offset adjust mode
                            if (_subtitleIndex == 0)
                            {
                                ToggleSubtitleOffsetAdjustMode();
                            }
                            // else: Do nothing, let FocusManager move focus up naturally
                        }
                    }
                    else if (_osdVisible) MoveOsdRow(-1);
                    else ShowAudioOverlay();
                    break;
                case AppKey.Down:
                    if (_audioOverlayVisible)
                    {
                        // Let FocusManager handle audio overlay navigation.
                    }
                    else if (_subtitleOverlayVisible) 
                    {
                        if (_subtitleOffsetAdjustMode) ExitSubtitleOffsetAdjustMode();
                        // else MoveSubtitleSelection(1); // Let FocusManager move focus down naturally.
                    }
                    else if (_osdVisible) MoveOsdRow(1);
                    else ShowSubtitleOverlay();
                    break;
                case AppKey.Back:
                    if (_subtitleOverlayVisible) 
                    {
                        if (_subtitleOffsetAdjustMode) ExitSubtitleOffsetAdjustMode();
                        else HideSubtitleOverlay();
                    }
                    else if (_audioOverlayVisible) HideAudioOverlay();
                    else if (_subtitleOffsetAdjustMode) ExitSubtitleOffsetAdjustMode();
                    else if (_isSeeking) { _isSeeking = false; UpdateProgress(); }
                    else if (_osdVisible) HideOSD();
                    else NavigationService.NavigateBack();
                    break;
            }
        }

        private void MoveOsdRow(int delta)
        {
            int newRow = Math.Clamp(_osdFocusRow + delta, 0, 1);
            if (newRow == _osdFocusRow) return;

            bool leavingButtonRow = _osdFocusRow == 1 && newRow != 1;
            _osdFocusRow = newRow;
            if (leavingButtonRow) ExitSubtitleOffsetAdjustMode();
            UpdateOsdFocus();
            _osdTimer.Stop();
            _osdTimer.Start();
        }

        private void MoveButtonFocus(int delta)
        {
            int maxIndex = Math.Max(0, _osdButtonCount - 1);
            int newIndex = Math.Clamp(_buttonFocusIndex + delta, 0, maxIndex);
            if (newIndex == _buttonFocusIndex) return;

            _buttonFocusIndex = newIndex;
            if (_subtitleOffsetAdjustMode) ExitSubtitleOffsetAdjustMode();
            UpdateOsdFocus();
            _osdTimer.Stop();
            _osdTimer.Start();
        }

        private void ActivateOsdButton()
        {
            switch (_buttonFocusIndex)
            {
                case 0: // Prev button
                    if (_movie.ItemType == "Episode") PlayPreviousEpisode();
                    break;
                case 1: // Next button
                    if (_movie.ItemType == "Episode") PlayNextEpisode();
                    break;
            }
        }
        
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
            {StopPlayback();
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
            catch (Exception ex) {NavigationService.NavigateBack(); }
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
            _useParsedSubtitleRenderer = false;
            _subtitleCues.Clear();
            _activeSubtitleCueIndex = -1;
            StopSubtitleRenderTimer();
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

