using System;
using System.Collections.Generic;
using Tizen.Multimedia;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using Tizen.NUI.Components;
using Tizen.Applications;
using JellyfinTizen.Core;
using JellyfinTizen.Models;
using JellyfinTizen.Utils;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http;
using System.Linq;
using System.Text.RegularExpressions;
using System.Net;
using System.Globalization;

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
        private TextLabel _clockLabel;
        private TextLabel _endsAtLabel;
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
        private int _subtitleTextBaseY;
        private int _subtitleTextOsdY;
        private Timer _subtitleRenderTimer;
        private List<SubtitleCue> _subtitleCues = new List<SubtitleCue>();
        private bool _useParsedSubtitleRenderer;
        private int _activeSubtitleCueIndex = -1;
        private Timer _reportProgressTimer;
        private bool _isFinished;
        private bool _completedForCurrentItem;
        private bool _isEpisodeSwitchInProgress;
        private bool _suppressStopReportOnce;
        private long _lastKnownPositionTicks;
        private View _progressThumb;
        private Timer _seekCommitTimer;
        private int _pendingSeekDeltaSeconds;
        private bool _isQueuedDirectionalSeekActive;
        private int _hiddenSeekBurstDirection;
        private int _hiddenSeekBurstCount;
        private DateTime _hiddenSeekLastPressUtc = DateTime.MinValue;
        private View _seekFeedbackContainer;
        private ImageView _seekFeedbackIcon;
        private TextLabel _seekFeedbackLabel;
        private View _playPauseFeedbackContainer;
        private ImageView _playFeedbackIcon;
        private ImageView _pauseFeedbackIcon;
        private Timer _playPauseFeedbackTimer;
        private Animation _playPauseFadeAnimation;
        private View _trickplayPreviewContainer;
        private ImageView _trickplayPreviewImage;
        private TrickplayInfo _trickplayInfo;
        private int _trickplayLastThumbnailIndex = -1;
        private int _trickplayLastTileIndex = -1;
        private int _trickplayUpdateToken;
        private string _trickplayCacheDir;
        private readonly Dictionary<int, string> _trickplayTileCache = new();
        private readonly Dictionary<int, Task<string>> _trickplayTileDownloads = new();
        private readonly object _trickplayTileLock = new();
        private HttpClient _trickplayHttpClient;
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
        private bool _suppressPlaybackCompletedNavigation;
        private View _smartActionPopup;
        private TextLabel _smartActionTitleLabel;
        private TextLabel _smartActionSubtitleLabel;
        private ImageView _smartActionIcon;
        private Timer _smartActionTimer;
        private Animation _smartActionPopupAnimation;
        private bool _smartPopupVisible;
        private bool _smartPopupFocused;
        private bool _smartPopupDismissedWhileHidden;
        private bool _isIntroPopupActive;
        private bool _isOutroPopupActive;
        private bool _introSkipped;
        private bool _autoNextTriggered;
        private bool _autoNextCancelledByBack;
        private int _nextEpisodeCountdownMs;
        private int _introEligibleSinceMs = -1;
        private int _outroEligibleSinceMs = -1;
        private SegmentWindow _introSegment;
        private SegmentWindow _outroSegment;
        private bool _hasIntroSegment;
        private bool _hasOutroSegment;

        // OSD Controls
        private View _controlsContainer;
        private View _subtitleOffsetButton;
        private View _audioButton;
        private View _subtitleButton;
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
        private Animation _osdAnimation;
        private Animation _topOsdAnimation;
        private Animation _subtitleOverlayAnimation;
        private Animation _audioOverlayAnimation;
        private Animation _subtitleTextAnimation;
        private Animation _seekFeedbackAnimation;
        private readonly Dictionary<View, Animation> _focusAnimations = new();
        private int _osdShownY;
        private int _osdHiddenY;
        private int _topOsdShownY;
        private int _topOsdHiddenY;
        private int _subtitleOverlayBaseX;
        private int _audioOverlayBaseX;
        private readonly string _sharedResPath = Application.Current.DirectoryInfo.SharedResource;

        private const int AudioButtonIndex = 0;
        private const int SubtitleButtonIndex = 1;
        private const int NextButtonIndex = 2;
        private const int SubtitleOffsetStepMs = 100;
        private const int SubtitleOffsetLimitMs = 5000;
        private const int SubtitleOffsetTrackWidth = 280;
        private const int SubtitleOsdLiftPx = 100;
        private const int OverlaySlideDistance = 36;
        private const int OsdSlideDistance = 34;
        private const int SeekStepSeconds = 10;
        private const int SeekCommitDelayMs = 700;
        private const int HiddenSeekBurstWindowMs = 500;
        private const int HiddenSeekLongPressCountThreshold = 4;
        private const int HiddenSeekLongPressRepeatMs = 130;
        private const int HiddenSeekLongPressRepeatCount = 3;
        private const int TrickplayPreviewWidth = 320;
        private const int TrickplayPreviewHeight = 180;
        private const int TrickplayPreviewBorderPx = 2;
        private const int TrickplayPreviewGapToSeekbar = 18;
        private const int SmartActionTickMs = 250;
        private const int IntroSkipSafetyMs = 600;
        private const int NextEpisodeAutoStartMs = 15000;
        private const int SmartPopupBreathingDelayMs = 1500;
        private const int SmartPopupMinWidth = 192;
        private const int SmartPopupMaxWidth = 362;
        private const int SmartPopupIntroHeight = 72;
        private const int SmartPopupOutroHeight = 104;
        private const int SmartPopupGapAboveSeekbar = 28;
        private const int TopOsdSidePadding = 60;

        // --- NEW: Store MediaSource and Override Audio ---
        private MediaSourceInfo _currentMediaSource;
        private int? _overrideAudioIndex = null;
        private bool _isAnamorphicVideo;
        private int _playbackToken;
        private string _activeReportItemId;
        private string _activeReportPlaySessionId;
        private string _activeReportMediaSourceId;
        private string _activeReportPlayMethod = "DirectPlay";
        private View _topOsdTitleView;

        private struct SubtitleCue
        {
            public int StartMs;
            public int EndMs;
            public string Text;
        }

        private struct SegmentWindow
        {
            public int StartMs;
            public int EndMs;
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
            _trickplayCacheDir = System.IO.Path.Combine(Application.Current.DirectoryInfo.Data, "trickplay-cache");

            // CRITICAL: Ensure the window is transparent so the video plane is visible.
            Window.Default.BackgroundColor = Color.Transparent;
            BackgroundColor = Color.Transparent;
        }

        public override void OnShow()
        {
            if (_osd == null) CreateOSD();

            _isFinished = false;
            _completedForCurrentItem = false;
            if (_reportProgressTimer != null)
            {
                _reportProgressTimer.Tick -= OnReportProgressTick;
                _reportProgressTimer.Stop();
            }
            _reportProgressTimer = new Timer(5000);
            _reportProgressTimer.Tick += OnReportProgressTick;
            _reportProgressTimer.Start();
            _smartActionTimer ??= new Timer(SmartActionTickMs);
            _smartActionTimer.Tick -= OnSmartActionTick;
            _smartActionTimer.Tick += OnSmartActionTick;
            _smartActionTimer.Start();

            StartPlayback();
        }

        public override void OnHide()
        {
            if (_reportProgressTimer != null)
            {
                _reportProgressTimer.Tick -= OnReportProgressTick;
                _reportProgressTimer.Stop();
            }
            _reportProgressTimer = null;
            ReportProgressToServer();
            UiAnimator.StopAndDispose(ref _osdAnimation);
            UiAnimator.StopAndDispose(ref _topOsdAnimation);
            UiAnimator.StopAndDispose(ref _subtitleOverlayAnimation);
            UiAnimator.StopAndDispose(ref _audioOverlayAnimation);
            UiAnimator.StopAndDispose(ref _subtitleTextAnimation);
            UiAnimator.StopAndDispose(ref _seekFeedbackAnimation);
            UiAnimator.StopAndDispose(ref _smartActionPopupAnimation);
            UiAnimator.StopAndDisposeAll(_focusAnimations);
            _seekCommitTimer?.Stop();
            _seekCommitTimer = null;
            _smartActionTimer?.Stop();
            StopPlayback();
            Window.Default.BackgroundColor = Color.Black;
            BackgroundColor = Color.Black;
        }

        private async void StartPlayback()
        {
            int playbackToken = ++_playbackToken;
            var playbackMovie = _movie;
            if (playbackMovie == null || string.IsNullOrWhiteSpace(playbackMovie.Id))
                return;

            try
            {
                _suppressPlaybackCompletedNavigation = false;
                _completedForCurrentItem = false;
                ResetSmartActionState();
                _subtitleEnabled = _initialSubtitleIndex.HasValue;
                _subtitleOffsetBurnInWarningShown = false;
                _useParsedSubtitleRenderer = false;
                _subtitleCues.Clear();
                _activeSubtitleCueIndex = -1;
                _player = new Player();

                _player.ErrorOccurred += OnPlayerErrorOccurred;
                _player.BufferingProgressChanged += OnBufferingProgressChanged;
                _player.PlaybackCompleted += OnPlaybackCompleted;
                _player.SubtitleUpdated += OnSubtitleUpdated;

                _player.Display = new Display(Window.Default);
                _player.DisplaySettings.Mode = PlayerDisplayMode.LetterBox;
                _player.DisplaySettings.IsVisible = true;

                var playbackInfo = await AppState.Jellyfin.GetPlaybackInfoAsync(playbackMovie.Id, _initialSubtitleIndex, _burnIn);
                if (playbackToken != _playbackToken)
                    return;

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
                _ = LoadMediaSegmentsAsync();

                try
                {
                    _isAnamorphicVideo = await AppState.Jellyfin.GetIsAnamorphicAsync(playbackMovie.Id);
                }
                catch
                {
                    _isAnamorphicVideo = false;
                }

                ApplyDisplayModeForCurrentVideo();
                _ = LoadTrickplayInfoAsync();

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
                    streamUrl = $"{serverUrl}/Videos/{playbackMovie.Id}/stream?static=true&MediaSourceId={mediaSource.Id}&PlaySessionId={_playSessionId}&api_key={apiKey}";
                }
                else if (supportsTranscoding || forceTranscode)
                {
                    _playMethod = "Transcode";

                    var videoStream = mediaSource.MediaStreams.FirstOrDefault(s => s.Type == "Video");
                    string vidCodec = videoStream?.Codec?.ToLower() ?? "unknown";
                    bool isVideoNative = vidCodec.Contains("h264") || vidCodec.Contains("hevc") || 
                                         vidCodec.Contains("vp9") || vidCodec.Contains("av1");
                    string container = isVideoNative ? "mp4" : "ts";
                    string audioPriority = "ac3,eac3,aac,mp3";
                    string requestedVideoCodecs = _burnIn ? "h264" : "h264,hevc,vp9,av1";
                    string requestedAudioCodecs = _burnIn ? "ac3,eac3,aac" : audioPriority;
                    const int fallbackMaxStreamingBitrate = 120000000;
                    const int fallbackAudioBitrate = 1411200;
                    int fallbackVideoBitrate = Math.Max(1000000, fallbackMaxStreamingBitrate - fallbackAudioBitrate);

                    if (!hasTranscodeUrl)
                    {
                        streamUrl = $"{serverUrl}/Videos/{playbackMovie.Id}/master.m3u8?MediaSourceId={mediaSource.Id}&PlaySessionId={_playSessionId}&api_key={apiKey}";
                        streamUrl += $"&VideoCodec={requestedVideoCodecs}";
                        streamUrl += $"&AudioCodec={requestedAudioCodecs}";
                        streamUrl += $"&SegmentContainer={container}"; 
                        streamUrl += "&TranscodingMaxAudioChannels=6";
                        streamUrl += $"&MaxStreamingBitrate={fallbackMaxStreamingBitrate}";
                        streamUrl += $"&VideoBitrate={fallbackVideoBitrate}";
                        streamUrl += $"&AudioBitrate={fallbackAudioBitrate}";
                        streamUrl += "&MinSegments=1";
                        streamUrl += "&BreakOnNonKeyFrames=True";
                    }
                    else
                    {
                        streamUrl = $"{serverUrl}{mediaSource.TranscodingUrl}";
                        if (_overrideAudioIndex.HasValue)
                        {
                            streamUrl = UpsertQueryParam(streamUrl, "SegmentContainer", container);
                            streamUrl = UpsertQueryParam(streamUrl, "AudioCodec", requestedAudioCodecs);
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
                        streamUrl = UpsertQueryParam(streamUrl, "AudioStreamIndex", _overrideAudioIndex.Value.ToString());

                    if (_initialSubtitleIndex.HasValue)
                    {
                        streamUrl = UpsertQueryParam(streamUrl, "SubtitleStreamIndex", _initialSubtitleIndex.Value.ToString());
                        if (_burnIn) streamUrl = UpsertQueryParam(streamUrl, "SubtitleMethod", "Encode");
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
                SetReportingContext(playbackMovie.Id, _playSessionId, mediaSource.Id, _playMethod);
                _player.SetSource(source);

                await _player.PrepareAsync();
                if (playbackToken != _playbackToken)
                    return;

                ApplyDisplayModeForCurrentVideo();

                try { _ = _player.StreamInfo.GetVideoProperties(); } catch { }
                try { _ = _player.AudioTrackInfo.GetCount(); } catch { }
                try 
                {
                    if (!_burnIn && _initialSubtitleIndex.HasValue)
                    {
                        var selectedSubStream = mediaSource.MediaStreams?
                            .FirstOrDefault(s => s.Type == "Subtitle" && s.Index == _initialSubtitleIndex.Value);
                        if (selectedSubStream != null)
                        {
                            bool useNativeEmbedded = _playMethod == "DirectPlay" && !selectedSubStream.IsExternal;
                            if (useNativeEmbedded)
                            {
                                if (!TrySelectNativeEmbeddedSubtitle(_initialSubtitleIndex.Value))
                                {
                                    await DownloadAndSetSubtitle(mediaSource.Id, selectedSubStream);
                                }
                            }
                            else
                            {
                                await DownloadAndSetSubtitle(mediaSource.Id, selectedSubStream);
                            }
                        }
                    }
                } catch {}

                if (_useParsedSubtitleRenderer) TryDisableNativeSubtitleTrack();

                if (_startPositionMs > 0)
                    await _player.SetPlayPositionAsync(_startPositionMs, false);

                _player.Start();
                if (playbackToken != _playbackToken)
                    return;

                ApplyDisplayModeForCurrentVideo();
                ApplySubtitleOffset();

                var info = new PlaybackProgressInfo
                {
                    ItemId = playbackMovie.Id, PlaySessionId = _playSessionId, MediaSourceId = mediaSource.Id,
                    PositionTicks = _startPositionMs * 10000, IsPaused = false, PlayMethod = _playMethod, EventName = "TimeUpdate"
                };
                _ = AppState.Jellyfin.ReportPlaybackStartAsync(info);
            }
            catch
            {
                if (playbackToken == _playbackToken)
                    ClearReportingContext();
            }
        }

        private async Task<bool> DownloadAndSetSubtitle(string mediaSourceId, MediaStream subtitleStream)
        {
            try
            {
                if (subtitleStream == null) return false;
                ApplySubtitleTextStyle();

                int subtitleIndex = subtitleStream.Index;
                var apiKey = AppState.AccessToken;
                var downloadUrl = BuildSubtitleDeliveryUrl(subtitleStream, mediaSourceId, subtitleIndex, "srt");
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    return false;
                }

                string ext = ResolveSubtitleExtension(downloadUrl, subtitleStream.Codec);
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

                bool preferParsedForTiming = !_burnIn && !string.Equals(_playMethod, "DirectPlay", StringComparison.OrdinalIgnoreCase);
                // Keep DirectPlay on native subtitle renderer for both embedded and external tracks.
                bool allowParsedRenderer = preferParsedForTiming;
                if (allowParsedRenderer && TryLoadSubtitleCues(localPath))
                {
                    _useParsedSubtitleRenderer = true;
                    TryDisableNativeSubtitleTrack();
                    StartSubtitleRenderTimer();
                }
                else
                {
                    _useParsedSubtitleRenderer = false;
                    _subtitleText?.Hide();
                    _player.SetSubtitle(localPath);
                }

                return true;
            }
            catch (Exception)
            {
                _externalSubtitlePath = null;
                _useParsedSubtitleRenderer = false;
                _subtitleCues.Clear();
                StopSubtitleRenderTimer();
                return false;
            }
        }

        private bool TryLoadSubtitleCues(string path)
        {
            if (TryLoadSrtSubtitleCues(path))
                return true;

            return TryLoadVttSubtitleCues(path);
        }

        private static string ResolveSubtitleExtension(string url, string codec)
        {
            string ext = null;

            if (!string.IsNullOrWhiteSpace(url))
            {
                string pathPart = NormalizeSubtitleDeliveryUrl(url);
                int queryIndex = pathPart.IndexOf('?');
                if (queryIndex >= 0) pathPart = pathPart.Substring(0, queryIndex);

                string candidate = System.IO.Path.GetExtension(pathPart);
                if (!string.IsNullOrWhiteSpace(candidate))
                    ext = candidate.TrimStart('.').ToLowerInvariant();
            }

            if (string.IsNullOrWhiteSpace(ext) && !string.IsNullOrWhiteSpace(codec))
                ext = codec.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(ext))
                ext = "srt";

            if (ext == "subrip")
                ext = "srt";

            return ext;
        }

        private bool TryLoadVttSubtitleCues(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;

                string raw = File.ReadAllText(path);
                string normalized = raw.Replace("\r\n", "\n").Replace('\r', '\n');
                string[] lines = normalized.Split('\n');
                var cues = new List<SubtitleCue>();

                int i = 0;
                while (i < lines.Length)
                {
                    string line = lines[i].Trim();
                    if (string.IsNullOrWhiteSpace(line) ||
                        line.StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("STYLE", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("NOTE", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("REGION", StringComparison.OrdinalIgnoreCase))
                    {
                        i++;
                        continue;
                    }

                    if (!line.Contains("-->"))
                    {
                        // Cue identifier line; timing should be on next line.
                        i++;
                        if (i >= lines.Length) break;
                        line = lines[i].Trim();
                    }

                    if (!line.Contains("-->"))
                    {
                        i++;
                        continue;
                    }

                    string[] parts = line.Split(new[] { "-->" }, StringSplitOptions.None);
                    if (parts.Length != 2)
                    {
                        i++;
                        continue;
                    }

                    if (!TryParseSubtitleTimestamp(parts[0], out int startMs))
                    {
                        i++;
                        continue;
                    }
                    if (!TryParseSubtitleTimestamp(parts[1], out int endMs))
                    {
                        i++;
                        continue;
                    }

                    if (endMs <= startMs) endMs = startMs + 500;

                    var cueLines = new List<string>();
                    i++;
                    while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
                    {
                        cueLines.Add(lines[i].TrimEnd());
                        i++;
                    }

                    string cueText = string.Join("\n", cueLines);
                    if (!string.IsNullOrWhiteSpace(cueText))
                    {
                        cues.Add(new SubtitleCue
                        {
                            StartMs = startMs,
                            EndMs = endMs,
                            Text = cueText
                        });
                    }
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

        private string BuildSubtitleDeliveryUrl(MediaStream subtitleStream, string mediaSourceId, int subtitleIndex, string ext)
        {
            if (subtitleStream == null) return null;

            var serverUrl = AppState.Jellyfin.ServerUrl?.TrimEnd('/');
            var apiKey = AppState.AccessToken;

            var deliveryUrl = subtitleStream.DeliveryUrl;
            if (!string.IsNullOrWhiteSpace(deliveryUrl))
            {
                string normalizedUrl = NormalizeSubtitleDeliveryUrl(deliveryUrl);
                string absoluteUrl;

                if (normalizedUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                {
                    // Parse pseudo file delivery URLs manually so query delimiters are preserved.
                    string raw = normalizedUrl.Substring("file://".Length).TrimStart('/');
                    int queryIndex = raw.IndexOf('?');
                    string path = queryIndex >= 0 ? raw.Substring(0, queryIndex) : raw;
                    string query = queryIndex >= 0 ? raw.Substring(queryIndex + 1) : string.Empty;

                    if (!path.StartsWith("Videos/", StringComparison.OrdinalIgnoreCase))
                        path = $"Videos/{path}";

                    absoluteUrl = $"{serverUrl}/{path}";
                    if (!string.IsNullOrWhiteSpace(query))
                        absoluteUrl = $"{absoluteUrl}?{query.TrimStart('?', '&')}";
                }
                else if (Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var absoluteUri))
                {
                    if (absoluteUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                        absoluteUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                    {
                        absoluteUrl = absoluteUri.ToString();
                    }
                    else
                    {
                        absoluteUrl = $"{serverUrl}/{normalizedUrl.TrimStart('/')}";
                    }
                }
                else
                {
                    absoluteUrl = $"{serverUrl}/{normalizedUrl.TrimStart('/')}";
                }

                absoluteUrl = NormalizeSubtitleDeliveryUrl(absoluteUrl);
                string lowerUrl = absoluteUrl.ToLowerInvariant();
                bool hasAuthParam = lowerUrl.Contains("api_key=") || lowerUrl.Contains("apikey=") || lowerUrl.Contains("token=");
                if (!hasAuthParam)
                {
                    absoluteUrl += absoluteUrl.Contains("?") ? $"&api_key={apiKey}" : $"?api_key={apiKey}";
                }
                return absoluteUrl;
            }

            // Safety fallback for older server payloads that do not include DeliveryUrl.
            return $"{serverUrl}/Videos/{_movie.Id}/{mediaSourceId}/Subtitles/{subtitleIndex}/0/Stream.{ext}?api_key={apiKey}";
        }

        private static string NormalizeSubtitleDeliveryUrl(string deliveryUrl)
        {
            if (string.IsNullOrWhiteSpace(deliveryUrl))
                return string.Empty;

            string normalized = deliveryUrl.Trim().Replace("\r", "").Replace("\n", "");
            normalized = normalized
                .Replace("%3F", "?", StringComparison.OrdinalIgnoreCase)
                .Replace("%26", "&", StringComparison.OrdinalIgnoreCase)
                .Replace("%3D", "=", StringComparison.OrdinalIgnoreCase)
                .Replace("??", "?")
                .Replace("?&", "?");
            return normalized;
        }

        private bool TrySelectNativeEmbeddedSubtitle(int jellyfinStreamIndex)
        {
            if (_player == null || _currentMediaSource == null)
                return false;

            if (_playMethod != "DirectPlay")
                return false;

            try
            {
                int tizenSubtitleCount = _player.SubtitleTrackInfo.GetCount();
                if (tizenSubtitleCount <= 0)
                    return false;

                var embeddedSubs = _currentMediaSource.MediaStreams?
                    .Where(s => s.Type == "Subtitle" && !s.IsExternal)
                    .OrderBy(s => s.Index)
                    .ToList();
                if (embeddedSubs == null || embeddedSubs.Count == 0)
                    return false;

                int tizenIndex = embeddedSubs.FindIndex(s => s.Index == jellyfinStreamIndex);
                if (tizenIndex < 0 || tizenIndex >= tizenSubtitleCount)
                {
                    return false;
                }

                _useParsedSubtitleRenderer = false;
                _subtitleCues.Clear();
                _activeSubtitleCueIndex = -1;
                StopSubtitleRenderTimer();
                _subtitleText?.Hide();
                _player.SubtitleTrackInfo.Selected = tizenIndex;
                _subtitleEnabled = true;
                _subtitleIndex = tizenIndex + 1;
                ApplySubtitleTextStyle();
                return true;
            }
            catch
            {
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
            int sidePadding = TopOsdSidePadding; int labelWidth = 140; int labelGap = 20; int bottomHeight = 260; int topHeight = 160; int screenWidth = Window.Default.Size.Width;
            _topOsdShownY = 0;
            _topOsdHiddenY = -OsdSlideDistance;
            _osdShownY = Window.Default.Size.Height - bottomHeight;
            _osdHiddenY = _osdShownY + OsdSlideDistance;

            _topOsd = new View { WidthResizePolicy = ResizePolicyType.FillToParent, HeightSpecification = topHeight, PositionY = _topOsdHiddenY, Opacity = 0.0f };
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

            _topOsdTitleView = CreateTopOsdTitleView(sidePadding);
            _topOsd.Add(_topOsdTitleView);
            _clockLabel = new TextLabel(FormatClockTime(DateTime.Now))
            {
                PositionX = screenWidth - sidePadding - 180,
                PositionY = 40,
                WidthSpecification = 180,
                HeightSpecification = 42,
                PointSize = 26,
                TextColor = Color.White,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.End
            };
            _topOsd.Add(_clockLabel);
            Add(_topOsd);

            _osd = new View { WidthResizePolicy = ResizePolicyType.FillToParent, HeightSpecification = bottomHeight, PositionY = _osdHiddenY, Opacity = 0.0f };
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
            _endsAtLabel = new TextLabel("")
            {
                PositionX = trackStartX,
                PositionY = 40,
                WidthSpecification = trackWidth,
                HeightSpecification = 36,
                PointSize = 23,
                TextColor = new Color(1, 1, 1, 0.9f),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.End
            };

            progressRow.Add(_currentTimeLabel);
            progressRow.Add(_progressTrack);
            progressRow.Add(_durationLabel);
            progressRow.Add(_progressThumb);
            progressRow.Add(_endsAtLabel);

            _controlsContainer = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 80,
                PositionY = 155,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    CellPadding = new Size2D(36, 0)
                }
            };
            _audioButton = CreateOsdButton("audio.svg", "Audio", 168);
            _subtitleButton = CreateOsdButton("sub.svg", "Subtitles", 206);
            _nextButton = CreateOsdButton("next.svg", "Next Episode", 242);

            _controlsContainer.Add(_audioButton);
            _controlsContainer.Add(_subtitleButton);
            _osdButtonCount = 2;
            _buttonFocusIndex = AudioButtonIndex;

            if (_movie.ItemType == "Episode")
            {
                _controlsContainer.Add(_nextButton);
                _osdButtonCount = 3;
            }

            CreateSubtitleOffsetTrack(screenWidth);
            UpdateSubtitleOffsetUI();

            _osd.Add(progressRow);
            _osd.Add(_subtitleOffsetTrackContainer);
            _osd.Add(_controlsContainer);
            Add(_osd);
            CreateTrickplayPreview();
            CreateSeekFeedback();
            CreatePlayPauseFeedback();
            CreateSmartActionPopup();

            CreateAudioOverlay();
            CreateSubtitleOverlay();
            CreateSubtitleText();
            
            _osdTimer = new Timer(5000);
            _osdTimer.Tick += OnOsdTimerTick;
            _progressTimer = new Timer(500);
            _progressTimer.Tick += (_, __) => { UpdateProgress(); return true; };
        }

        private static string UpsertQueryParam(string url, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key))
                return url;

            string baseUrl = url;
            string query = string.Empty;
            int queryStart = url.IndexOf('?');
            if (queryStart >= 0)
            {
                baseUrl = url.Substring(0, queryStart);
                if (queryStart < url.Length - 1)
                    query = url.Substring(queryStart + 1);
            }

            var kept = new List<string>();
            if (!string.IsNullOrWhiteSpace(query))
            {
                foreach (var part in query.Split('&'))
                {
                    if (string.IsNullOrWhiteSpace(part))
                        continue;

                    int eq = part.IndexOf('=');
                    string k = eq >= 0 ? part.Substring(0, eq) : part;
                    if (k.Equals(key, StringComparison.OrdinalIgnoreCase))
                        continue;

                    kept.Add(part);
                }
            }

            kept.Add($"{key}={value}");
            return $"{baseUrl}?{string.Join("&", kept)}";
        }

        private bool OnOsdTimerTick(object sender, Timer.TickEventArgs e)
        {
            // Never auto-hide while seek preview is active; otherwise preview can snap back to current time.
            if (_isSeeking || _isQueuedDirectionalSeekActive)
                return true;

            HideOSD();
            return false;
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

        private void RefreshTopOsdTitleView()
        {
            if (_topOsd == null)
                return;

            try
            {
                if (_topOsdTitleView != null)
                    _topOsd.Remove(_topOsdTitleView);
            }
            catch
            {
            }

            _topOsdTitleView = CreateTopOsdTitleView(TopOsdSidePadding);
            _topOsd.Add(_topOsdTitleView);

            if (_clockLabel != null)
                _clockLabel.RaiseToTop();
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

        private View CreateOsdButton(string iconFile, string text, int width)
        {
            var btn = new View
            {
                WidthSpecification = width,
                HeightSpecification = 60,
                BackgroundColor = new Color(1f, 1f, 1f, 0.10f),
                CornerRadius = 28.0f
            };

            var content = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    CellPadding = new Size2D(10, 0)
                }
            };

            var icon = new ImageView
            {
                WidthSpecification = 28,
                HeightSpecification = 28,
                ResourceUrl = ResolveFreshIconPath(iconFile),
                FittingMode = FittingModeType.ShrinkToFit,
                SamplingMode = SamplingModeType.BoxThenLanczos
            };

            var label = new TextLabel(text)
            {
                HeightResizePolicy = ResizePolicyType.FillToParent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextColor = Color.White,
                PointSize = 26
            };

            content.Add(icon);
            content.Add(label);
            btn.Add(content);
            return btn;
        }

        private string ResolveFreshIconPath(string iconFile)
        {
            string fallback = System.IO.Path.Combine(_sharedResPath, iconFile);

            try
            {
                var candidates = new List<string>();

                string sharedPath = System.IO.Path.Combine(Application.Current.DirectoryInfo.SharedResource, iconFile);
                if (File.Exists(sharedPath))
                    candidates.Add(sharedPath);

                string appResPath = System.IO.Path.Combine(Application.Current.DirectoryInfo.Resource, iconFile);
                if (File.Exists(appResPath))
                    candidates.Add(appResPath);

                if (candidates.Count == 0)
                    return fallback;

                string sourcePath = candidates[0];
                DateTime sourceWrite = File.GetLastWriteTimeUtc(sourcePath);
                for (int i = 1; i < candidates.Count; i++)
                {
                    DateTime currentWrite = File.GetLastWriteTimeUtc(candidates[i]);
                    if (currentWrite > sourceWrite)
                    {
                        sourceWrite = currentWrite;
                        sourcePath = candidates[i];
                    }
                }

                string cacheDir = System.IO.Path.Combine(Application.Current.DirectoryInfo.Data, "icon-cache");
                Directory.CreateDirectory(cacheDir);

                string name = System.IO.Path.GetFileNameWithoutExtension(iconFile);
                string ext = System.IO.Path.GetExtension(iconFile);
                string versionedPath = System.IO.Path.Combine(cacheDir, $"{name}_{sourceWrite.Ticks}{ext}");

                if (!File.Exists(versionedPath))
                    File.Copy(sourcePath, versionedPath, overwrite: true);

                return versionedPath;
            }
            catch
            {
                return fallback;
            }
        }

        private void CreateTrickplayPreview()
        {
            _trickplayPreviewContainer = new View
            {
                WidthSpecification = TrickplayPreviewWidth + (TrickplayPreviewBorderPx * 2),
                HeightSpecification = TrickplayPreviewHeight + (TrickplayPreviewBorderPx * 2),
                BackgroundColor = Color.White,
                CornerRadius = 12.0f,
                ClippingMode = ClippingModeType.ClipChildren,
                PositionY = Window.Default.Size.Height - 460,
                Opacity = 1.0f
            };

            _trickplayPreviewImage = new ImageView
            {
                WidthSpecification = TrickplayPreviewWidth,
                HeightSpecification = TrickplayPreviewHeight,
                PositionX = TrickplayPreviewBorderPx,
                PositionY = TrickplayPreviewBorderPx,
                FittingMode = FittingModeType.Fill,
                SamplingMode = SamplingModeType.BoxThenLanczos,
                PreMultipliedAlpha = false,
                CornerRadius = 10.0f
            };

            _trickplayPreviewContainer.Add(_trickplayPreviewImage);
            _trickplayPreviewContainer.Hide();
            Add(_trickplayPreviewContainer);
        }

        private void CreateSeekFeedback()
        {
            int screenWidth = Window.Default.Size.Width;
            int screenHeight = Window.Default.Size.Height;

            _seekFeedbackContainer = new View
            {
                WidthSpecification = 180,
                HeightSpecification = 180,
                PositionX = (int)(screenWidth * 0.70f) - 90,
                PositionY = (screenHeight / 2) - 90,
                BackgroundColor = new Color(0f, 0f, 0f, 0.28f),
                CornerRadius = 24.0f,
                Opacity = 0.0f,
                Scale = Vector3.One
            };

            var content = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    CellPadding = new Size2D(0, 10)
                }
            };

            _seekFeedbackIcon = new ImageView
            {
                WidthSpecification = 74,
                HeightSpecification = 74,
                FittingMode = FittingModeType.ShrinkToFit,
                SamplingMode = SamplingModeType.BoxThenLanczos
            };

            _seekFeedbackLabel = new TextLabel("10s")
            {
                HeightSpecification = 44,
                PointSize = 24,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            content.Add(_seekFeedbackIcon);
            content.Add(_seekFeedbackLabel);
            _seekFeedbackContainer.Add(content);
            _seekFeedbackContainer.Hide();
            Add(_seekFeedbackContainer);
        }

        private void ShowSeekFeedback(int direction, int seekSeconds)
        {
            if (_seekFeedbackContainer == null || _seekFeedbackIcon == null || _seekFeedbackLabel == null)
                return;

            _seekFeedbackIcon.ResourceUrl = direction > 0
                ? _sharedResPath + "forward.svg"
                : _sharedResPath + "reverse.svg";
            _seekFeedbackLabel.Text = $"{Math.Abs(seekSeconds)}s";
            _seekFeedbackContainer.PositionX = direction > 0
                ? (int)(Window.Default.Size.Width * 0.70f) - 90
                : (int)(Window.Default.Size.Width * 0.30f) - 90;

            _seekFeedbackContainer.Show();
            _seekFeedbackContainer.Opacity = 0.86f;
            _seekFeedbackContainer.Scale = new Vector3(0.98f, 0.98f, 1f);

            UiAnimator.Replace(
                ref _seekFeedbackAnimation,
                UiAnimator.Start(
                    130,
                    animation =>
                    {
                        animation.AnimateTo(_seekFeedbackContainer, "Opacity", 1.0f);
                        animation.AnimateTo(_seekFeedbackContainer, "Scale", new Vector3(1.06f, 1.06f, 1f));
                    }
                )
            );
        }

        private void HideSeekFeedback()
        {
            if (_seekFeedbackContainer == null)
                return;

            UiAnimator.Replace(
                ref _seekFeedbackAnimation,
                UiAnimator.Start(
                    140,
                    animation =>
                    {
                        animation.AnimateTo(_seekFeedbackContainer, "Opacity", 0.0f);
                        animation.AnimateTo(_seekFeedbackContainer, "Scale", new Vector3(0.98f, 0.98f, 1f));
                    },
                    () => _seekFeedbackContainer.Hide()
                )
            );
        }

        private void CreatePlayPauseFeedback()
        {
            int screenWidth = Window.Default.Size.Width;
            int screenHeight = Window.Default.Size.Height;

            _playPauseFeedbackContainer = new View
            {
                WidthSpecification = 140,
                HeightSpecification = 140,
                PositionX = (screenWidth / 2) - 70,
                PositionY = (screenHeight / 2) - 70,
                BackgroundColor = new Color(0f, 0f, 0f, 0.34f),
                CornerRadius = 70.0f,
                Opacity = 0.0f,
                Scale = new Vector3(0.9f, 0.9f, 1f)
            };

            _playFeedbackIcon = new ImageView
            {
                WidthSpecification = 90,
                HeightSpecification = 90,
                PositionX = 25,
                PositionY = 25,
                ResourceUrl = _sharedResPath + "play.svg",
                FittingMode = FittingModeType.ShrinkToFit,
                SamplingMode = SamplingModeType.BoxThenLanczos
            };

            _pauseFeedbackIcon = new ImageView
            {
                WidthSpecification = 90,
                HeightSpecification = 90,
                PositionX = 25,
                PositionY = 25,
                ResourceUrl = _sharedResPath + "pause.svg",
                FittingMode = FittingModeType.ShrinkToFit,
                SamplingMode = SamplingModeType.BoxThenLanczos
            };

            _playPauseFeedbackContainer.Add(_playFeedbackIcon);
            _playPauseFeedbackContainer.Add(_pauseFeedbackIcon);
            _playFeedbackIcon.Hide();
            _pauseFeedbackIcon.Hide();
            _playPauseFeedbackContainer.Hide();
            Add(_playPauseFeedbackContainer);
        }

        private void ShowPlayPauseFeedback(bool isPaused)
        {
            if (_playPauseFeedbackContainer == null || _playFeedbackIcon == null || _pauseFeedbackIcon == null)
                return;

            if (isPaused)
            {
                _playFeedbackIcon.Hide();
                _pauseFeedbackIcon.Show();
            }
            else
            {
                _pauseFeedbackIcon.Hide();
                _playFeedbackIcon.Show();
            }

            _playPauseFeedbackContainer.Show();
            _playPauseFeedbackContainer.Opacity = 0.0f;
            _playPauseFeedbackContainer.Scale = Vector3.One;

            UiAnimator.Replace(
                ref _playPauseFadeAnimation,
                UiAnimator.Start(
                    130,
                    animation => animation.AnimateTo(_playPauseFeedbackContainer, "Opacity", 1.0f)
                )
            );

            _playPauseFeedbackTimer ??= new Timer(600);
            _playPauseFeedbackTimer.Stop();
            _playPauseFeedbackTimer.Tick -= OnPlayPauseFeedbackTimerTick;
            _playPauseFeedbackTimer.Tick += OnPlayPauseFeedbackTimerTick;
            _playPauseFeedbackTimer.Start();
        }

        private bool OnPlayPauseFeedbackTimerTick(object sender, Timer.TickEventArgs e)
        {
            _playPauseFeedbackTimer?.Stop();
            if (_playPauseFeedbackContainer != null)
            {
                UiAnimator.Replace(
                    ref _playPauseFadeAnimation,
                    UiAnimator.Start(
                        180,
                        animation => animation.AnimateTo(_playPauseFeedbackContainer, "Opacity", 0.0f),
                        () => _playPauseFeedbackContainer.Hide()
                    )
                );
            }
            return false;
        }

        private void CreateSmartActionPopup()
        {
            _smartActionPopup = new View
            {
                WidthSpecification = SmartPopupMinWidth,
                HeightSpecification = SmartPopupIntroHeight,
                BackgroundColor = new Color(0.16f, 0.18f, 0.22f, 0.95f),
                CornerRadius = 8.0f,
                Opacity = 0.0f,
                Scale = new Vector3(0.98f, 0.98f, 1f)
            };

            var content = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent
            };

            _smartActionTitleLabel = new TextLabel("")
            {
                PositionX = 28,
                WidthSpecification = SmartPopupMinWidth - 60,
                HeightSpecification = SmartPopupIntroHeight,
                PointSize = 27,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Begin,
                VerticalAlignment = VerticalAlignment.Center
            };

            _smartActionSubtitleLabel = new TextLabel("")
            {
                PositionX = 28,
                PositionY = 56,
                WidthSpecification = SmartPopupMinWidth - 52,
                HeightSpecification = 36,
                PointSize = 21,
                TextColor = new Color(1f, 1f, 1f, 0.92f),
                HorizontalAlignment = HorizontalAlignment.Begin,
                VerticalAlignment = VerticalAlignment.Center
            };
            _smartActionSubtitleLabel.Hide();

            _smartActionIcon = new ImageView
            {
                WidthSpecification = 30,
                HeightSpecification = 30,
                PositionX = SmartPopupMinWidth - 44,
                PositionY = (SmartPopupIntroHeight - 30) / 2,
                ResourceUrl = ResolveFreshIconPath("next.svg"),
                FittingMode = FittingModeType.ShrinkToFit,
                SamplingMode = SamplingModeType.BoxThenLanczos
            };

            content.Add(_smartActionTitleLabel);
            content.Add(_smartActionSubtitleLabel);
            content.Add(_smartActionIcon);
            _smartActionPopup.Add(content);
            UpdateSmartPopupPosition();
            _smartActionPopup.Hide();
            Add(_smartActionPopup);
        }

        private void UpdateSmartPopupPosition()
        {
            if (_smartActionPopup == null)
                return;

            // Seekbar absolute Y: OSD panel anchor + progress row + track offset.
            int seekbarY = _osdShownY + 90 + 22;
            int popupHeight = _smartActionPopup.HeightSpecification;
            int targetY = seekbarY - popupHeight - SmartPopupGapAboveSeekbar;
            _smartActionPopup.PositionY = Math.Max(40, targetY);

            // Align popup right edge with seekbar line right edge (not with duration label).
            int trackStartX = 60 + 140 + 20;
            int trackWidth = Window.Default.Size.Width - (2 * trackStartX);
            int seekbarRightX = trackStartX + trackWidth;
            _smartActionPopup.PositionX = seekbarRightX - _smartActionPopup.WidthSpecification;
        }

        private static int EstimateSmartPopupTextWidth(string text, float perCharPx)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 100;

            return Math.Clamp((int)Math.Ceiling(text.Length * perCharPx), 70, 520);
        }

        private void UpdateSmartPopupContentLayout(string title, string subtitle, bool isIntro)
        {
            if (_smartActionPopup == null || _smartActionTitleLabel == null || _smartActionSubtitleLabel == null || _smartActionIcon == null)
                return;

            const int leftPadding = 28;
            const int iconWidth = 30;
            bool hasSubtitle = !string.IsNullOrWhiteSpace(subtitle);
            int textIconGap = 8;
            int rightPadding = isIntro ? 12 : 14;

            float titleCharWidth = isIntro ? 11.8f : 15.2f;
            int titleWidth = EstimateSmartPopupTextWidth(title, titleCharWidth);
            int subtitleWidth = string.IsNullOrWhiteSpace(subtitle) ? 0 : EstimateSmartPopupTextWidth(subtitle, 10.8f);
            int primaryLineWidth = leftPadding + titleWidth + textIconGap + iconWidth + rightPadding;
            int secondaryLineWidth = leftPadding + subtitleWidth + rightPadding;
            int desiredWidth = Math.Max(primaryLineWidth, secondaryLineWidth);
            int popupWidth = Math.Clamp(desiredWidth, SmartPopupMinWidth, SmartPopupMaxWidth);

            int maxTitleWidth = Math.Max(82, popupWidth - leftPadding - textIconGap - iconWidth - rightPadding);
            int maxSubtitleWidth = Math.Max(102, popupWidth - leftPadding - rightPadding);
            int actualTitleWidth = Math.Min(titleWidth, maxTitleWidth);
            _smartActionPopup.WidthSpecification = popupWidth;
            _smartActionPopup.HeightSpecification = hasSubtitle ? SmartPopupOutroHeight : SmartPopupIntroHeight;

            _smartActionTitleLabel.WidthSpecification = maxTitleWidth;
            _smartActionTitleLabel.HeightSpecification = hasSubtitle ? 46 : SmartPopupIntroHeight;
            _smartActionTitleLabel.PositionY = hasSubtitle ? 12 : 0;

            _smartActionIcon.WidthSpecification = iconWidth;
            _smartActionIcon.HeightSpecification = iconWidth;
            _smartActionIcon.PositionX = leftPadding + actualTitleWidth + textIconGap;
            int titleLineY = (int)Math.Round(_smartActionTitleLabel.PositionY);
            int titleLineH = _smartActionTitleLabel.HeightSpecification;
            int iconHeight = _smartActionIcon.HeightSpecification;
            int iconOpticalOffsetY = isIntro ? 2 : 1;
            _smartActionIcon.PositionY = titleLineY + Math.Max(0, ((titleLineH - iconHeight) / 2)) + iconOpticalOffsetY;

            _smartActionSubtitleLabel.WidthSpecification = maxSubtitleWidth;
            _smartActionSubtitleLabel.Text = subtitle ?? string.Empty;
            if (!hasSubtitle)
                _smartActionSubtitleLabel.Hide();
            else
                _smartActionSubtitleLabel.Show();
        }

        private void SetSmartPopupFocus(bool focused)
        {
            _smartPopupFocused = focused && _smartPopupVisible;
            if (_smartActionPopup == null)
                return;

            _smartActionPopup.BackgroundColor = _smartPopupFocused
                ? new Color(0.24f, 0.27f, 0.33f, 0.98f)
                : new Color(0.16f, 0.18f, 0.22f, 0.95f);
            AnimateFocusScale(_smartActionPopup, _smartPopupFocused ? new Vector3(1.04f, 1.04f, 1f) : Vector3.One);
        }

        private void ShowSmartPopup(string title, string subtitle, bool isIntro, bool focused)
        {
            if (_smartActionPopup == null || _smartActionTitleLabel == null || _smartActionIcon == null)
                return;

            _smartActionTitleLabel.Text = title ?? string.Empty;
            UpdateSmartPopupContentLayout(title, subtitle, isIntro);
            _isIntroPopupActive = isIntro;
            _isOutroPopupActive = !isIntro;
            UpdateSmartPopupPosition();

            if (_smartPopupVisible)
            {
                SetSmartPopupFocus(focused);
                return;
            }

            _smartPopupVisible = true;
            _smartActionPopup.Show();
            _smartActionPopup.Opacity = 0.0f;
            _smartActionPopup.Scale = new Vector3(0.98f, 0.98f, 1f);

            UiAnimator.Replace(
                ref _smartActionPopupAnimation,
                UiAnimator.Start(
                    140,
                    animation =>
                    {
                        animation.AnimateTo(_smartActionPopup, "Opacity", 1.0f);
                        animation.AnimateTo(_smartActionPopup, "Scale", Vector3.One);
                    }
                )
            );
            SetSmartPopupFocus(focused);
        }

        private void HideSmartPopup()
        {
            _isIntroPopupActive = false;
            _isOutroPopupActive = false;
            _smartPopupFocused = false;
            if (_smartActionPopup == null)
                return;

            if (!_smartPopupVisible)
            {
                _smartActionPopup.Hide();
                return;
            }

            _smartPopupVisible = false;
            UiAnimator.Replace(
                ref _smartActionPopupAnimation,
                UiAnimator.Start(
                    130,
                    animation =>
                    {
                        animation.AnimateTo(_smartActionPopup, "Opacity", 0.0f);
                        animation.AnimateTo(_smartActionPopup, "Scale", new Vector3(0.98f, 0.98f, 1f));
                    },
                    () => _smartActionPopup.Hide()
                )
            );
        }

        private async Task LoadMediaSegmentsAsync()
        {
            try
            {
                var segments = await AppState.Jellyfin.GetMediaSegmentsAsync(_movie.Id, "Intro", "Outro");
                if (segments == null || segments.Count == 0)
                {
                    return;
                }

                var intro = SelectSegmentWindow(segments, "Intro");
                if (intro.HasValue)
                {
                    _introSegment = intro.Value;
                    _hasIntroSegment = true;
                }

                var outro = SelectSegmentWindow(segments, "Outro");
                if (outro.HasValue)
                {
                    _outroSegment = outro.Value;
                    _hasOutroSegment = true;
                }
            }
            catch
            {
                _hasIntroSegment = false;
                _hasOutroSegment = false;
            }
        }

        private static SegmentWindow? SelectSegmentWindow(List<MediaSegmentInfo> segments, string type)
        {
            if (segments == null || segments.Count == 0 || string.IsNullOrWhiteSpace(type))
                return null;

            var segment = segments
                .Where(s => string.Equals(s.Type, type, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.StartTicks)
                .FirstOrDefault();
            if (segment == null)
                return null;

            int startMs = segment.StartTicks > 0 ? (int)(segment.StartTicks / 10000) : 0;
            int endMs = segment.EndTicks > 0 ? (int)(segment.EndTicks / 10000) : 0;
            if (endMs <= startMs)
                endMs = startMs + 20000;

            return new SegmentWindow
            {
                StartMs = Math.Max(0, startMs),
                EndMs = Math.Max(startMs + 1000, endMs)
            };
        }

        private bool OnSmartActionTick(object sender, Timer.TickEventArgs e)
        {
            EvaluateSmartActions();
            return true;
        }

        private void EvaluateSmartActions()
        {
            if (_player == null || _isFinished || _isEpisodeSwitchInProgress)
            {
                _introEligibleSinceMs = -1;
                _outroEligibleSinceMs = -1;
                HideSmartPopup();
                return;
            }

            int positionMs = GetPlayPositionMs();
            if (positionMs < 0)
            {
                _introEligibleSinceMs = -1;
                _outroEligibleSinceMs = -1;
                HideSmartPopup();
                return;
            }

            bool canShowIntro = _hasIntroSegment &&
                                !_introSkipped &&
                                positionMs >= _introSegment.StartMs &&
                                positionMs < Math.Max(_introSegment.StartMs + 400, _introSegment.EndMs - IntroSkipSafetyMs);
            if (canShowIntro)
            {
                if (_introEligibleSinceMs < 0)
                    _introEligibleSinceMs = positionMs;

                bool delayDone = (positionMs - _introEligibleSinceMs) >= SmartPopupBreathingDelayMs;
                if (delayDone && (!_smartPopupDismissedWhileHidden || _osdVisible))
                    ShowSmartPopup("Skip Intro", null, isIntro: true, focused: _smartPopupFocused || !_osdVisible);
                return;
            }
            _introEligibleSinceMs = -1;

            bool canShowOutro = _movie.ItemType == "Episode" &&
                                _hasOutroSegment &&
                                !_autoNextTriggered &&
                                positionMs >= _outroSegment.StartMs;
            if (!canShowOutro)
            {
                _outroEligibleSinceMs = -1;
                _nextEpisodeCountdownMs = 0;
                _autoNextCancelledByBack = false;
                _smartPopupDismissedWhileHidden = false;
                HideSmartPopup();
                return;
            }

            if (_outroEligibleSinceMs < 0)
                _outroEligibleSinceMs = positionMs;

            bool outroDelayDone = (positionMs - _outroEligibleSinceMs) >= SmartPopupBreathingDelayMs;
            if (!outroDelayDone)
                return;

            if (_nextEpisodeCountdownMs <= 0)
                _nextEpisodeCountdownMs = NextEpisodeAutoStartMs;

            // User dismissed hidden-state popup: keep it gone in hidden mode.
            // If OSD is opened manually, expose a manual-only Next Episode button (no timer/autoplay).
            if (_smartPopupDismissedWhileHidden)
            {
                if (!_osdVisible)
                {
                    HideSmartPopup();
                    return;
                }

                ShowSmartPopup("Next Episode", null, isIntro: false, focused: _smartPopupFocused);
                return;
            }

            int seconds = (int)Math.Ceiling(_nextEpisodeCountdownMs / 1000.0);
            ShowSmartPopup("Next Episode", $"Starts in {Math.Max(1, seconds)} seconds", isIntro: false, focused: _smartPopupFocused || !_osdVisible);

            if (_player.State == PlayerState.Playing && _smartPopupVisible)
                _nextEpisodeCountdownMs = Math.Max(0, _nextEpisodeCountdownMs - SmartActionTickMs);

            if (_nextEpisodeCountdownMs > 0)
                return;

            if (!_smartPopupVisible)
                return;

            if (_autoNextCancelledByBack)
                return;

            _autoNextTriggered = true;
            HideSmartPopup();
            PlayNextEpisode();
        }

        private async void SkipIntro()
        {
            if (_player == null || !_hasIntroSegment)
                return;

            int duration = GetDuration();
            int targetMs = _introSegment.EndMs + IntroSkipSafetyMs;
            if (duration > 0)
                targetMs = Math.Min(targetMs, Math.Max(0, duration - 1000));
            targetMs = Math.Max(targetMs, 0);

            try
            {
                _introSkipped = true;
                _isSeeking = true;
                _seekPreviewMs = targetMs;
                UpdatePreviewBar();
                await _player.SetPlayPositionAsync(targetMs, false);
            }
            catch
            {
            }
            finally
            {
                _isSeeking = false;
                UpdateProgress();
                HideSmartPopup();
            }
        }

        private bool HandleSmartPopupEnter()
        {
            if (!_smartPopupVisible)
                return false;

            if (_isIntroPopupActive)
            {
                SkipIntro();
                return true;
            }

            if (_isOutroPopupActive && _movie.ItemType == "Episode")
            {
                // Hidden OSD popup should activate on Enter.
                // With OSD visible, require explicit popup focus.
                if (_osdVisible && !_smartPopupFocused)
                    return false;

                _autoNextTriggered = true;
                _autoNextCancelledByBack = false;
                HideSmartPopup();
                PlayNextEpisode();
                return true;
            }

            return false;
        }

        private void ResetSmartActionState()
        {
            _hasIntroSegment = false;
            _hasOutroSegment = false;
            _smartPopupFocused = false;
            _smartPopupDismissedWhileHidden = false;
            _introSkipped = false;
            _autoNextTriggered = false;
            _autoNextCancelledByBack = false;
            _nextEpisodeCountdownMs = 0;
            _introEligibleSinceMs = -1;
            _outroEligibleSinceMs = -1;
            _introSegment = default;
            _outroSegment = default;
            HideSmartPopup();
        }

        private void SetButtonVisual(View button, bool focused)
        {
             if (button == null) return;
             button.BackgroundColor = focused
                 ? new Color(0.82f, 0.82f, 0.82f, 0.34f)
                 : new Color(1f, 1f, 1f, 0.10f);
             AnimateFocusScale(button, focused ? new Vector3(1.08f, 1.08f, 1f) : Vector3.One);
        }

        private void AnimateFocusScale(View view, Vector3 targetScale)
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
            catch (Exception)
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
                
                AnimateFocusScale(_subtitleOffsetButton, _subtitleOffsetAdjustMode ? new Vector3(1.05f, 1.05f, 1f) : Vector3.One);
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
                AnimateFocusScale(_subtitleOffsetButton, Vector3.One);
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
                HideTrickplayPreview();
            }

            _currentTimeLabel.Text = FormatTime(position);
            int remainingMs = Math.Max(duration - position, 0);
            _durationLabel.Text = $"-{FormatTime(remainingMs)}";
            if (_clockLabel != null) _clockLabel.Text = FormatClockTime(DateTime.Now);
            if (_endsAtLabel != null)
            {
                DateTime endAt = DateTime.Now.AddMilliseconds(remainingMs);
                _endsAtLabel.Text = $"Ends at {FormatClockTime(endAt)}";
            }
        }

        private string FormatTime(int ms)
        {
            var t = TimeSpan.FromMilliseconds(ms);
            return t.Hours > 0 ? $"{t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}" : $"{t.Minutes:D2}:{t.Seconds:D2}";
        }

        private string FormatClockTime(DateTime time)
        {
            // Tizen locale/font stacks can render lowercase am/pm on some TVs.
            // Force invariant uppercase AM/PM for consistent UI across devices.
            return time.ToString("h:mm tt", CultureInfo.InvariantCulture).ToUpperInvariant();
        }

        private void BeginSeek()
        {
            if (_player == null)
                return;

            _isSeeking = true;
            _seekPreviewMs = GetPlayPositionMs();
            UpdatePreviewBar();
        }

        private async Task CommitSeekAsync(int visualHoldMs = 0)
        {
            if (!_isSeeking || _player == null) return;
            try
            {
                if (visualHoldMs > 0)
                {
                    await Task.Delay(visualHoldMs);
                }
                var seekTask = _player.SetPlayPositionAsync(_seekPreviewMs, false);
                await Task.WhenAny(seekTask, Task.Delay(3000));
            }
            catch {}
            finally
            {
                _isSeeking = false;
                _pendingSeekDeltaSeconds = 0;
                _isQueuedDirectionalSeekActive = false;
                _hiddenSeekBurstCount = 0;
                _hiddenSeekBurstDirection = 0;
                HideSeekFeedback();
                HideTrickplayPreview();
                UpdateProgress();
            }
        }

        private void Scrub(int seconds)
        {
            if (!_isSeeking) BeginSeek();
            _seekPreviewMs += seconds * 1000;
            _seekPreviewMs = Math.Clamp(_seekPreviewMs, 0, GetDuration());
            UpdatePreviewBar();
            ShowOSD();
        }

        private void CancelQueuedDirectionalSeek()
        {
            _seekCommitTimer?.Stop();
            _pendingSeekDeltaSeconds = 0;
            _isQueuedDirectionalSeekActive = false;
            _hiddenSeekBurstCount = 0;
            _hiddenSeekBurstDirection = 0;
            HideSeekFeedback();
            HideTrickplayPreview();
        }

        private void HandleHiddenDirectionalSeek(int direction)
        {
            if (_player == null || direction == 0)
                return;

            var now = DateTime.UtcNow;
            var elapsedMs = _hiddenSeekLastPressUtc == DateTime.MinValue
                ? double.MaxValue
                : (now - _hiddenSeekLastPressUtc).TotalMilliseconds;

            // If the next repeat arrives only after the 0.5s window, treat as long-press behavior.
            if (_isQueuedDirectionalSeekActive &&
                direction == _hiddenSeekBurstDirection &&
                elapsedMs > HiddenSeekBurstWindowMs)
            {
                CancelQueuedDirectionalSeek();
                _isSeeking = false;
                Scrub(direction * 30);
                _hiddenSeekLastPressUtc = now;
                return;
            }

            if (_isSeeking && !_isQueuedDirectionalSeekActive)
            {
                Scrub(direction * 30);
                return;
            }

            bool isBurst = direction == _hiddenSeekBurstDirection &&
                           (now - _hiddenSeekLastPressUtc).TotalMilliseconds <= HiddenSeekBurstWindowMs;

            if (isBurst) _hiddenSeekBurstCount++;
            else
            {
                _hiddenSeekBurstDirection = direction;
                _hiddenSeekBurstCount = 1;
            }
            _hiddenSeekLastPressUtc = now;

            // Repeated very-fast events are likely a held key repeat (long press).
            if (isBurst &&
                elapsedMs <= HiddenSeekLongPressRepeatMs &&
                _hiddenSeekBurstCount >= HiddenSeekLongPressRepeatCount)
            {
                CancelQueuedDirectionalSeek();
                _isSeeking = false;
                Scrub(direction * 30);
                return;
            }

            if (_hiddenSeekBurstCount >= HiddenSeekLongPressCountThreshold)
            {
                CancelQueuedDirectionalSeek();
                _isSeeking = false;
                Scrub(direction * 30);
                return;
            }

            QueueDirectionalSeek(direction);
        }

        private void QueueDirectionalSeek(int direction)
        {
            if (_player == null || direction == 0)
                return;

            if (!_isSeeking)
                BeginSeek();

            _isQueuedDirectionalSeekActive = true;
            _pendingSeekDeltaSeconds += direction * SeekStepSeconds;
            _seekPreviewMs += direction * SeekStepSeconds * 1000;
            _seekPreviewMs = Math.Clamp(_seekPreviewMs, 0, GetDuration());

            if (_pendingSeekDeltaSeconds == 0)
            {
                _isSeeking = false;
                CancelQueuedDirectionalSeek();
                UpdateProgress();
                return;
            }

            ShowSeekFeedback(Math.Sign(_pendingSeekDeltaSeconds), _pendingSeekDeltaSeconds);
            UpdatePreviewBar();

            _seekCommitTimer ??= new Timer(SeekCommitDelayMs);
            _seekCommitTimer.Stop();
            _seekCommitTimer.Tick -= OnSeekCommitTimerTick;
            _seekCommitTimer.Tick += OnSeekCommitTimerTick;
            _seekCommitTimer.Start();
        }

        private bool OnSeekCommitTimerTick(object sender, Timer.TickEventArgs e)
        {
            _seekCommitTimer?.Stop();
            _ = CommitSeekAsync(220);
            return false;
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
            UpdateTrickplayPreviewPosition();
            _ = UpdateTrickplayPreviewAsync();
        }

        private async Task LoadTrickplayInfoAsync()
        {
            try
            {
                _trickplayInfo = await AppState.Jellyfin.GetTrickplayInfoAsync(_movie.Id);
                _trickplayLastThumbnailIndex = -1;
                _trickplayLastTileIndex = -1;
                _trickplayUpdateToken++;
                if (_trickplayInfo == null)
                    HideTrickplayPreview();
            }
            catch
            {
                _trickplayInfo = null;
                HideTrickplayPreview();
            }
        }

        private async Task UpdateTrickplayPreviewAsync()
        {
            if (!_isSeeking || !_osdVisible || _trickplayInfo == null || _trickplayPreviewImage == null || _trickplayPreviewContainer == null)
            {
                HideTrickplayPreview();
                return;
            }

            int intervalMs = Math.Max(1, _trickplayInfo.IntervalMs);
            int thumbIndex = Math.Max(0, _seekPreviewMs / intervalMs);
            if (_trickplayInfo.ThumbnailCount > 0)
                thumbIndex = Math.Min(thumbIndex, _trickplayInfo.ThumbnailCount - 1);

            int thumbsPerTile = Math.Max(1, _trickplayInfo.TileWidth * _trickplayInfo.TileHeight);
            int tileIndex = thumbIndex / thumbsPerTile;
            int cellIndex = thumbIndex % thumbsPerTile;
            int col = cellIndex % _trickplayInfo.TileWidth;
            int row = cellIndex / _trickplayInfo.TileWidth;

            float cellWidth = 1f / _trickplayInfo.TileWidth;
            float cellHeight = 1f / _trickplayInfo.TileHeight;
            var pixelArea = new Vector4(col * cellWidth, row * cellHeight, cellWidth, cellHeight);

            RunOnUiThread(() =>
            {
                _trickplayPreviewImage.PixelArea = pixelArea;
                UpdateTrickplayPreviewPosition();
                _trickplayPreviewContainer.Show();
            });

            if (thumbIndex == _trickplayLastThumbnailIndex && tileIndex == _trickplayLastTileIndex)
                return;

            _trickplayLastThumbnailIndex = thumbIndex;
            int token = ++_trickplayUpdateToken;

            var tilePath = await GetOrDownloadTrickplayTileAsync(tileIndex);
            if (string.IsNullOrWhiteSpace(tilePath))
                return;

            if (token != _trickplayUpdateToken || !_isSeeking)
                return;

            _trickplayLastTileIndex = tileIndex;
            RunOnUiThread(() =>
            {
                _trickplayPreviewImage.ResourceUrl = tilePath;
                _trickplayPreviewImage.PreMultipliedAlpha = false;
                _trickplayPreviewImage.PixelArea = pixelArea;
            });

            _ = GetOrDownloadTrickplayTileAsync(tileIndex + 1);
        }

        private async Task<string> GetOrDownloadTrickplayTileAsync(int tileIndex)
        {
            if (_trickplayInfo == null || tileIndex < 0)
                return null;

            lock (_trickplayTileLock)
            {
                if (_trickplayTileCache.TryGetValue(tileIndex, out var cachedPath) &&
                    File.Exists(cachedPath))
                {
                    return cachedPath;
                }
            }

            Task<string> downloadTask;
            lock (_trickplayTileLock)
            {
                if (!_trickplayTileDownloads.TryGetValue(tileIndex, out downloadTask))
                {
                    downloadTask = DownloadTrickplayTileAsync(tileIndex);
                    _trickplayTileDownloads[tileIndex] = downloadTask;
                }

            }

            var path = await downloadTask;

            lock (_trickplayTileLock)
            {
                _trickplayTileDownloads.Remove(tileIndex);
                if (!string.IsNullOrWhiteSpace(path))
                    _trickplayTileCache[tileIndex] = path;
            }

            return path;
        }

        private async Task<string> DownloadTrickplayTileAsync(int tileIndex)
        {
            if (_trickplayInfo == null || string.IsNullOrWhiteSpace(_movie?.Id) || string.IsNullOrWhiteSpace(AppState.ServerUrl))
                return null;

            try
            {
                Directory.CreateDirectory(_trickplayCacheDir);

                string mediaSourceId = _currentMediaSource?.Id ?? "default";
                string fileName = $"{_movie.Id}_{mediaSourceId}_{_trickplayInfo.Width}_{tileIndex}.jpg";
                foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                    fileName = fileName.Replace(c, '_');

                string localPath = System.IO.Path.Combine(_trickplayCacheDir, fileName);
                if (File.Exists(localPath) && new FileInfo(localPath).Length > 0)
                    return localPath;

                var serverUrl = AppState.ServerUrl.TrimEnd('/');
                var token = Uri.EscapeDataString(AppState.AccessToken ?? string.Empty);
                var url = $"{serverUrl}/Videos/{_movie.Id}/Trickplay/{_trickplayInfo.Width}/{tileIndex}.jpg?api_key={token}";
                if (!string.IsNullOrWhiteSpace(_currentMediaSource?.Id))
                    url += $"&MediaSourceId={Uri.EscapeDataString(_currentMediaSource.Id)}";

                _trickplayHttpClient ??= new HttpClient();
                var bytes = await _trickplayHttpClient.GetByteArrayAsync(url);
                if (bytes == null || bytes.Length < 64)
                    return null;

                await File.WriteAllBytesAsync(localPath, bytes);
                return localPath;
            }
            catch
            {
                return null;
            }
        }

        private void UpdateTrickplayPreviewPosition()
        {
            if (_trickplayPreviewContainer == null)
                return;

            int duration = GetDuration();
            if (duration <= 0)
                return;

            int trackStartX = 60 + 140 + 20;
            int trackWidth = Window.Default.Size.Width - (2 * trackStartX);
            float ratio = Math.Clamp((float)_seekPreviewMs / duration, 0f, 1f);
            int trackX = trackStartX + (int)Math.Floor(trackWidth * ratio);

            int previewWidth = TrickplayPreviewWidth + (TrickplayPreviewBorderPx * 2);
            int previewX = trackX - (previewWidth / 2);
            int minX = 20;
            int maxX = Window.Default.Size.Width - previewWidth - 20;
            _trickplayPreviewContainer.PositionX = Math.Clamp(previewX, minX, Math.Max(minX, maxX));

            int seekBarY = _osdShownY + 90 + 22;
            int previewHeight = TrickplayPreviewHeight + (TrickplayPreviewBorderPx * 2);
            _trickplayPreviewContainer.PositionY = seekBarY - previewHeight - TrickplayPreviewGapToSeekbar;
        }

        private void HideTrickplayPreview()
        {
            RunOnUiThread(() => _trickplayPreviewContainer?.Hide());
        }

        private void ResetTrickplayState()
        {
            _trickplayInfo = null;
            _trickplayLastThumbnailIndex = -1;
            _trickplayLastTileIndex = -1;
            _trickplayUpdateToken++;
            HideTrickplayPreview();
            lock (_trickplayTileLock)
            {
                _trickplayTileCache.Clear();
                _trickplayTileDownloads.Clear();
            }
        }

        private void CreateSubtitleOverlay()
        {
            _subtitleOverlayBaseX = Window.Default.Size.Width - 500;
            _subtitleOverlay = new View { WidthSpecification = 450, HeightSpecification = 500, BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.95f), PositionX = _subtitleOverlayBaseX + OverlaySlideDistance, PositionY = Window.Default.Size.Height - 780, CornerRadius = 16.0f, ClippingMode = ClippingModeType.ClipChildren, Opacity = 0.0f };
            _subtitleOverlay.Hide();
            Add(_subtitleOverlay);
        }

        private void CreateSubtitleText()
        {
            _subtitleText = new TextLabel("")
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word,
                EnableMarkup = false
            };
            ApplySubtitleTextStyle();
            _subtitleText.Hide();
            Add(_subtitleText);
        }

        private void ApplySubtitleTextStyle()
        {
            if (_subtitleText == null)
                return;

            // Keep styling identical across embedded and sidecar paths.
            _subtitleText.TextColor = Color.White;
            _subtitleText.HorizontalAlignment = HorizontalAlignment.Center;
            _subtitleText.VerticalAlignment = VerticalAlignment.Center;
            _subtitleText.BackgroundColor = Color.Transparent;
            _subtitleText.PointSize = 48;
            _subtitleText.HeightSpecification = 180;
            _subtitleText.Padding = new Extents(180, 180, 0, 0);
            _subtitleTextBaseY = Window.Default.Size.Height - 270;
            try
            {
                _subtitleText.SetFontStyle(new Tizen.NUI.Text.FontStyle { Weight = FontWeightType.Normal });
            }
            catch { }

            _subtitleTextOsdY = _subtitleTextBaseY - SubtitleOsdLiftPx;
            _subtitleText.PositionY = _osdVisible ? _subtitleTextOsdY : _subtitleTextBaseY;
        }

        private void ShowSubtitleOverlay()
        {
            if (_player == null) return;
            if (_subtitleOverlay == null) CreateSubtitleOverlay();
            HideAudioOverlay();
            ExitSubtitleOffsetAdjustMode();
            var wasVisible = _subtitleOverlayVisible;

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
                
                if (!_burnIn)
                {
                    // OFF option is only meaningful when subtitles are sidecar/native, not burn-in.
                    var offRow = CreateSubtitleRow("OFF", "OFF_INDEX");
                    _subtitleListContainer.Add(offRow);
                }

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
            ScrollSubtitleSelectionIntoView();
            _subtitleOverlay.Show();
            _subtitleOverlayVisible = true;

            if (!wasVisible)
            {
                _subtitleOverlay.PositionX = _subtitleOverlayBaseX + OverlaySlideDistance;
                _subtitleOverlay.Opacity = 0.0f;

                UiAnimator.Replace(
                    ref _subtitleOverlayAnimation,
                    UiAnimator.Start(
                        UiAnimator.PanelDurationMs,
                        animation =>
                        {
                            animation.AnimateTo(_subtitleOverlay, "PositionX", (float)_subtitleOverlayBaseX);
                            animation.AnimateTo(_subtitleOverlay, "Opacity", 1.0f);
                        }
                    )
                );
            }
            else
            {
                _subtitleOverlay.PositionX = _subtitleOverlayBaseX;
                _subtitleOverlay.Opacity = 1.0f;
            }
        }

        private View CreateSubtitleRow(string text, string indexId)
        {
            var row = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent, HeightSpecification = 60,
                BackgroundColor = Color.Transparent, CornerRadius = 8.0f,
                Margin = new Extents(20, 20, 5, 5),
                Focusable = false
            };
            row.Name = indexId; 
            
            var label = new TextLabel(text) { WidthResizePolicy = ResizePolicyType.FillToParent, HeightResizePolicy = ResizePolicyType.FillToParent, PointSize = 28, TextColor = new Color(1, 1, 1, 0.6f), HorizontalAlignment = HorizontalAlignment.Begin, VerticalAlignment = VerticalAlignment.Center, Padding = new Extents(20, 0, 0, 0) };
            row.Add(label);
            return row;
        }

        private void UpdateSubtitleVisuals(bool setFocus = true)
        {
            if (_subtitleListContainer == null) return;
            int count = (int)_subtitleListContainer.ChildCount;
            for (int i = 0; i < count; i++)
            {
                var row = _subtitleListContainer.GetChildAt((uint)i);
                bool selected = (i == _subtitleIndex);
                row.BackgroundColor = selected ? new Color(1, 1, 1, 0.25f) : Color.Transparent;
                AnimateFocusScale(row, selected ? new Vector3(1.05f, 1.05f, 1.0f) : Vector3.One);
                var label = row.GetChildAt(0) as TextLabel;
                if (label != null)
                {
                    label.TextColor = selected ? Color.White : new Color(1, 1, 1, 0.6f);
                }
            }
            // Keep subtitle selection fully manual; FocusManager + ScrollableBase can crash on some TVs.
        }

        private void MoveSubtitleSelection(int delta)
        {
            if (!_subtitleOverlayVisible || _subtitleListContainer == null) return;
            int count = (int)_subtitleListContainer.ChildCount;
            if (count == 0) return;
            _subtitleIndex = Math.Clamp(_subtitleIndex + delta, 0, count - 1);
            UpdateSubtitleVisuals(setFocus: false);
            ScrollSubtitleSelectionIntoView();
        }

        private void ScrollSubtitleSelectionIntoView()
        {
            if (_subtitleListContainer == null || _subtitleScrollView == null)
                return;

            int count = (int)_subtitleListContainer.ChildCount;
            if (count <= 0 || _subtitleIndex < 0 || _subtitleIndex >= count)
                return;

            var selected = _subtitleListContainer.GetChildAt((uint)_subtitleIndex);
            int rowTop = (int)Math.Round(selected.PositionY);
            int rowBottom = rowTop + (int)Math.Round(selected.SizeHeight);

            int viewportHeight = _subtitleScrollView.SizeHeight > 0
                ? (int)Math.Round(_subtitleScrollView.SizeHeight)
                : 320;
            int currentTop = (int)(-_subtitleListContainer.PositionY);
            int currentBottom = currentTop + viewportHeight;

            int nextTop = currentTop;
            if (rowBottom > currentBottom)
                nextTop = rowBottom - viewportHeight + 8;
            else if (rowTop < currentTop)
                nextTop = Math.Max(0, rowTop - 8);

            _subtitleListContainer.PositionY = -nextTop;
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
                _initialSubtitleIndex = null;
                _useParsedSubtitleRenderer = false;
                _subtitleCues.Clear();
                _activeSubtitleCueIndex = -1;
                StopSubtitleRenderTimer();
                _subtitleHideTimer?.Stop();
                try { _player?.ClearSubtitle(); } catch { }
                RunOnUiThread(() =>
                {
                    _subtitleText?.Hide();
                });

                if (_burnIn)
                {
                    long currentPos = GetPlayPositionMs();
                    _suppressStopReportOnce = true;
                    StopPlayback();
                    _startPositionMs = (int)currentPos;
                    StartPlayback();
                }
                return;
            }

            if (!int.TryParse(selectedName, out int jellyfinStreamIndex)) return;
            _subtitleEnabled = true;

            var subStream = _currentMediaSource?.MediaStreams?.FirstOrDefault(s => s.Index == jellyfinStreamIndex);
            if (subStream == null) return;

            _initialSubtitleIndex = jellyfinStreamIndex;
            bool sidecarSet = false;
            if (_burnIn)
            {
                long currentPos = GetPlayPositionMs();
                _suppressStopReportOnce = true;
                StopPlayback();
                _startPositionMs = (int)currentPos;
                StartPlayback();
                return;
            }

            if (!_burnIn)
            {
                bool useNativeEmbedded = _playMethod == "DirectPlay" && !subStream.IsExternal;
                if (useNativeEmbedded)
                {
                    if (TrySelectNativeEmbeddedSubtitle(jellyfinStreamIndex))
                    {
                        ApplySubtitleOffset();
                        return;
                    }
                    sidecarSet = await DownloadAndSetSubtitle(_currentMediaSource.Id, subStream);
                }
                else
                {
                    sidecarSet = await DownloadAndSetSubtitle(_currentMediaSource.Id, subStream);
                }

                if (sidecarSet)
                {
                    ApplySubtitleOffset();
                    return;
                }
            }
        }

        private void CreateAudioOverlay()
        {
            _audioOverlayBaseX = Window.Default.Size.Width - 500;
            _audioOverlay = new View { WidthSpecification = 450, HeightSpecification = 500, BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.95f), PositionX = _audioOverlayBaseX + OverlaySlideDistance, PositionY = Window.Default.Size.Height - 780, CornerRadius = 16.0f, ClippingMode = ClippingModeType.ClipChildren, Opacity = 0.0f };
            _audioOverlay.Hide();
            Add(_audioOverlay);
        }

        private void ShowAudioOverlay()
        {
            if (_player == null) return;
            if (_audioOverlay == null) CreateAudioOverlay();
            HideSubtitleOverlay();
            ExitSubtitleOffsetAdjustMode();
            var wasVisible = _audioOverlayVisible;

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

                    var row = new View { WidthResizePolicy = ResizePolicyType.FillToParent, HeightSpecification = 60, BackgroundColor = Color.Transparent, CornerRadius = 8.0f, Margin = new Extents(20, 20, 5, 5), Focusable = false };
                    row.Name = stream.Index.ToString();
                    var label = new TextLabel(displayText) { WidthResizePolicy = ResizePolicyType.FillToParent, HeightResizePolicy = ResizePolicyType.FillToParent, PointSize = 26, TextColor = new Color(1, 1, 1, 0.6f), HorizontalAlignment = HorizontalAlignment.Begin, VerticalAlignment = VerticalAlignment.Center, Padding = new Extents(20, 0, 0, 0) };
                    row.Add(label);
                    _audioListContainer.Add(row);
                }
            }
            UpdateAudioVisuals();
            ScrollAudioSelectionIntoView();
            _audioOverlay.Show();
            _audioOverlayVisible = true;

            if (!wasVisible)
            {
                _audioOverlay.PositionX = _audioOverlayBaseX + OverlaySlideDistance;
                _audioOverlay.Opacity = 0.0f;

                UiAnimator.Replace(
                    ref _audioOverlayAnimation,
                    UiAnimator.Start(
                        UiAnimator.PanelDurationMs,
                        animation =>
                        {
                            animation.AnimateTo(_audioOverlay, "PositionX", (float)_audioOverlayBaseX);
                            animation.AnimateTo(_audioOverlay, "Opacity", 1.0f);
                        }
                    )
                );
            }
            else
            {
                _audioOverlay.PositionX = _audioOverlayBaseX;
                _audioOverlay.Opacity = 1.0f;
            }
        }

        private void UpdateAudioVisuals(bool setFocus = true)
        {
            if (_audioListContainer == null) return;
            int count = (int)_audioListContainer.ChildCount;
            for (int i = 0; i < count; i++)
            {
                var row = _audioListContainer.GetChildAt((uint)i);
                bool selected = (i == _audioIndex);
                row.BackgroundColor = selected ? new Color(1, 1, 1, 0.25f) : Color.Transparent;
                AnimateFocusScale(row, selected ? new Vector3(1.05f, 1.05f, 1.0f) : Vector3.One);
                var label = row.GetChildAt(0) as TextLabel;
                if (label != null) label.TextColor = selected ? Color.White : new Color(1, 1, 1, 0.6f);
            }
            // Keep audio selection fully manual; device FocusManager + ScrollableBase can crash on some TVs.
        }

        private void ScrollAudioSelectionIntoView()
        {
            if (_audioListContainer == null || _audioScrollView == null)
                return;

            int count = (int)_audioListContainer.ChildCount;
            if (count <= 0 || _audioIndex < 0 || _audioIndex >= count)
                return;

            var selected = _audioListContainer.GetChildAt((uint)_audioIndex);
            int rowTop = (int)Math.Round(selected.PositionY);
            int rowBottom = rowTop + (int)Math.Round(selected.SizeHeight);

            int viewportHeight = _audioScrollView.SizeHeight > 0
                ? (int)Math.Round(_audioScrollView.SizeHeight)
                : 420;
            int currentTop = (int)(-_audioListContainer.PositionY);
            int currentBottom = currentTop + viewportHeight;

            int nextTop = currentTop;
            if (rowBottom > currentBottom)
                nextTop = rowBottom - viewportHeight + 8;
            else if (rowTop < currentTop)
                nextTop = Math.Max(0, rowTop - 8);

            _audioListContainer.PositionY = -nextTop;
        }

        private void HideAudioOverlay()
        {
            if (_audioOverlay == null)
                return;

            if (!_audioOverlayVisible)
            {
                _audioOverlay.Hide();
                return;
            }

            _audioOverlayVisible = false;

            UiAnimator.Replace(
                ref _audioOverlayAnimation,
                UiAnimator.Start(
                    UiAnimator.PanelDurationMs,
                    animation =>
                    {
                        animation.AnimateTo(_audioOverlay, "PositionX", _audioOverlayBaseX + OverlaySlideDistance);
                        animation.AnimateTo(_audioOverlay, "Opacity", 0.0f);
                    },
                    () => _audioOverlay.Hide()
                )
            );
        }

        private void HideSubtitleOverlay()
        {
            if (_subtitleOverlay == null)
                return;

            if (!_subtitleOverlayVisible)
            {
                _subtitleOverlay.Hide();
                return;
            }

            _subtitleOverlayVisible = false;

            UiAnimator.Replace(
                ref _subtitleOverlayAnimation,
                UiAnimator.Start(
                    UiAnimator.PanelDurationMs,
                    animation =>
                    {
                        animation.AnimateTo(_subtitleOverlay, "PositionX", _subtitleOverlayBaseX + OverlaySlideDistance);
                        animation.AnimateTo(_subtitleOverlay, "Opacity", 0.0f);
                    },
                    () => _subtitleOverlay.Hide()
                )
            );
        }

        private void OnSubtitleUpdated(object sender, SubtitleUpdatedEventArgs e)
        {
            if (_useParsedSubtitleRenderer)
            {
                return;
            }

            RunOnUiThread(() =>
            {
                string text = e.Text;
                string normalizedText = NormalizeParsedCueText(text);
                _subtitleHideTimer?.Stop();

                if (_subtitleText == null || !_subtitleEnabled || string.IsNullOrWhiteSpace(normalizedText))
                {
                    _subtitleText?.Hide();
                    return;
                }

                _subtitleText.Text = normalizedText;
                _subtitleText.Show();

                uint hideDurationMs = (uint)Math.Clamp((long)e.Duration, 200L, uint.MaxValue);
                _subtitleHideTimer = new Timer(hideDurationMs);
                _subtitleHideTimer.Tick += (_, __) => { _subtitleText.Hide(); return false; };
                _subtitleHideTimer.Start();
            });
        }

        
        private void MoveAudioSelection(int delta) 
        { 
            if (_audioOverlay == null || !_audioOverlayVisible || _audioListContainer == null) return; 
            int count = (int)_audioListContainer.ChildCount;
            if (count <= 0) return; 
            _audioIndex = Math.Clamp(_audioIndex + delta, 0, count - 1); 
            UpdateAudioVisuals(setFocus: false);
            ScrollAudioSelectionIntoView();
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
            _suppressStopReportOnce = true;
            StopPlayback();
            _overrideAudioIndex = jellyfinStreamIndex;
            _startPositionMs = (int)currentPos;
            StartPlayback();
        }

        private void ShowOSD()
        {
            var wasVisible = _osdVisible;
            if (!wasVisible) _osdFocusRow = 0;
            if (!wasVisible) SetSmartPopupFocus(false);

            _osd.Show();
            if (_topOsd != null) _topOsd.Show();

            if (_subtitleOffsetTrackContainer != null)
            {
                if (_subtitleOffsetAdjustMode) _subtitleOffsetTrackContainer.Show();
                else _subtitleOffsetTrackContainer.Hide();
            }
            _osdVisible = true;

            if (!wasVisible)
            {
                _osd.PositionY = _osdHiddenY;
                _osd.Opacity = 0.0f;

                UiAnimator.Replace(
                    ref _osdAnimation,
                    UiAnimator.Start(
                        UiAnimator.PanelDurationMs,
                        animation =>
                        {
                            animation.AnimateTo(_osd, "PositionY", (float)_osdShownY);
                            animation.AnimateTo(_osd, "Opacity", 1.0f);
                        }
                    )
                );

                if (_topOsd != null)
                {
                    _topOsd.PositionY = _topOsdHiddenY;
                    _topOsd.Opacity = 0.0f;

                    UiAnimator.Replace(
                        ref _topOsdAnimation,
                        UiAnimator.Start(
                            UiAnimator.PanelDurationMs,
                            animation =>
                            {
                                animation.AnimateTo(_topOsd, "PositionY", (float)_topOsdShownY);
                                animation.AnimateTo(_topOsd, "Opacity", 1.0f);
                            }
                        )
                    );
                }

                if (_subtitleText != null)
                {
                    UiAnimator.Replace(
                        ref _subtitleTextAnimation,
                        UiAnimator.AnimateTo(_subtitleText, "PositionY", (float)_subtitleTextOsdY, UiAnimator.ScrollDurationMs)
                    );
                }
            }
            else
            {
                _osd.PositionY = _osdShownY;
                _osd.Opacity = 1.0f;
                if (_topOsd != null)
                {
                    _topOsd.PositionY = _topOsdShownY;
                    _topOsd.Opacity = 1.0f;
                }
            }

            UpdateOsdFocus();
            UpdateSubtitleOffsetUI();
            UpdateProgress();
            _osdTimer.Stop(); _osdTimer.Start();
            _progressTimer.Start();
        }

        private void UpdateOsdFocus()
        {
            if (_progressTrack == null) return;

            bool buttonRowFocused = _osdFocusRow == 1;
            _progressTrack.BackgroundColor = buttonRowFocused
                ? new Color(1, 1, 1, 0.25f)
                : new Color(1, 1, 1, 0.4f);

            SetButtonVisual(_audioButton, buttonRowFocused && _buttonFocusIndex == AudioButtonIndex);
            SetButtonVisual(_subtitleButton, buttonRowFocused && _buttonFocusIndex == SubtitleButtonIndex);
            SetButtonVisual(_nextButton, buttonRowFocused && _buttonFocusIndex == NextButtonIndex);

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
            _osdVisible = false;
            SetSmartPopupFocus(false);

            UiAnimator.Replace(
                ref _osdAnimation,
                UiAnimator.Start(
                    UiAnimator.PanelDurationMs,
                    animation =>
                    {
                        animation.AnimateTo(_osd, "PositionY", (float)_osdHiddenY);
                        animation.AnimateTo(_osd, "Opacity", 0.0f);
                    },
                    () => _osd.Hide()
                )
            );

            if (_topOsd != null)
            {
                UiAnimator.Replace(
                    ref _topOsdAnimation,
                    UiAnimator.Start(
                        UiAnimator.PanelDurationMs,
                        animation =>
                        {
                            animation.AnimateTo(_topOsd, "PositionY", (float)_topOsdHiddenY);
                            animation.AnimateTo(_topOsd, "Opacity", 0.0f);
                        },
                        () => _topOsd.Hide()
                    )
                );
            }

            if (_subtitleText != null)
            {
                UiAnimator.Replace(
                    ref _subtitleTextAnimation,
                    UiAnimator.AnimateTo(_subtitleText, "PositionY", (float)_subtitleTextBaseY, UiAnimator.ScrollDurationMs)
                );
            }

            _osdTimer.Stop();
            _progressTimer.Stop();
            HideTrickplayPreview();

            if (_smartPopupVisible && (_isIntroPopupActive || _isOutroPopupActive) && !_smartPopupDismissedWhileHidden)
                SetSmartPopupFocus(true);
        }

        private bool OnReportProgressTick(object sender, Timer.TickEventArgs e) { ReportProgressToServer(force: false); return true; }

        private void SetReportingContext(string itemId, string playSessionId, string mediaSourceId, string playMethod)
        {
            _activeReportItemId = itemId;
            _activeReportPlaySessionId = playSessionId;
            _activeReportMediaSourceId = mediaSourceId;
            _activeReportPlayMethod = string.IsNullOrWhiteSpace(playMethod) ? "DirectPlay" : playMethod;
            _lastKnownPositionTicks = Math.Max(0, (long)_startPositionMs) * 10000;
        }

        private void ClearReportingContext()
        {
            _activeReportItemId = null;
            _activeReportPlaySessionId = null;
            _activeReportMediaSourceId = null;
            _activeReportPlayMethod = "DirectPlay";
            _lastKnownPositionTicks = 0;
        }

        private async Task ReportPlaybackStoppedSafeAsync(PlaybackProgressInfo info)
        {
            try
            {
                await AppState.Jellyfin.ReportPlaybackStoppedAsync(info);
            }
            catch
            {
            }
        }

        private void ReportProgressToServer(bool force = false)
        {
            if (_player == null || _isSeeking || _isFinished || _currentMediaSource == null) return;
            if (!force && _player.State != PlayerState.Playing) return;
            if (string.IsNullOrWhiteSpace(_activeReportItemId) || string.IsNullOrWhiteSpace(_activeReportMediaSourceId))
                return;

            var positionMs = GetPlayPositionMs();
            var durationMs = GetDuration();
            if (durationMs <= 0) return;

            var info = new PlaybackProgressInfo
            {
                ItemId = _activeReportItemId, PlaySessionId = _activeReportPlaySessionId, MediaSourceId = _activeReportMediaSourceId,
                PositionTicks = (long)positionMs * 10000, IsPaused = _player.State == PlayerState.Paused,
                PlayMethod = _activeReportPlayMethod, EventName = force ? (_player.State == PlayerState.Paused ? "Pause" : "Unpause") : "TimeUpdate"
            };
            _lastKnownPositionTicks = info.PositionTicks;
            _ = AppState.Jellyfin.ReportPlaybackProgressAsync(info);

            if (((double)positionMs / durationMs) > 0.95 && !_isFinished)
            {
                if (!_completedForCurrentItem)
                    FinalizeCurrentItemAsPlayed();
            }
        }

        private void FinalizeCurrentItemAsPlayed()
        {
            var completedItemId = _activeReportItemId ?? _movie?.Id;
            if (string.IsNullOrWhiteSpace(completedItemId))
                return;

            _completedForCurrentItem = true;
            _ = AppState.Jellyfin.MarkAsPlayedAsync(completedItemId);
            _ = AppState.Jellyfin.UpdatePlaybackPositionAsync(completedItemId, 0);
        }

        private void OnPlaybackCompleted(object sender, EventArgs e)
        {
            if (_suppressPlaybackCompletedNavigation)
                return;

            _suppressPlaybackCompletedNavigation = true;
            _isFinished = true;
            FinalizeCurrentItemAsPlayed();
            RunOnUiThread(() => { if (_movie.ItemType == "Episode") PlayNextEpisode(); else NavigationService.NavigateBack(); });
        }

        private void StopPlayback()
        {
            _playbackToken++;
            bool suppressStopReport = _suppressStopReportOnce || _isEpisodeSwitchInProgress;
            _suppressStopReportOnce = false;
            _suppressPlaybackCompletedNavigation = true;
            _osdVisible = false;
            _audioOverlayVisible = false;
            _subtitleOverlayVisible = false;
            _subtitleOffsetAdjustMode = false;
            UiAnimator.StopAndDispose(ref _osdAnimation);
            UiAnimator.StopAndDispose(ref _topOsdAnimation);
            UiAnimator.StopAndDispose(ref _subtitleOverlayAnimation);
            UiAnimator.StopAndDispose(ref _audioOverlayAnimation);
            UiAnimator.StopAndDispose(ref _subtitleTextAnimation);
            UiAnimator.StopAndDispose(ref _seekFeedbackAnimation);
            UiAnimator.StopAndDispose(ref _playPauseFadeAnimation);
            UiAnimator.StopAndDisposeAll(_focusAnimations);
            _playPauseFeedbackTimer?.Stop();
            _seekCommitTimer?.Stop();
            _pendingSeekDeltaSeconds = 0;
            _isQueuedDirectionalSeekActive = false;
            _isSeeking = false;
            _seekPreviewMs = 0;
            _seekFeedbackContainer?.Hide();
            HideTrickplayPreview();
            HideSmartPopup();
            _playPauseFeedbackContainer?.Hide();
            _audioOverlay?.Hide();
            _subtitleOverlay?.Hide();
            _subtitleOffsetTrackContainer?.Hide();
            _osd?.Hide();
            _topOsd?.Hide();
            ResetTrickplayState();
            _trickplayHttpClient?.Dispose();
            _trickplayHttpClient = null;
            ResetSmartActionState();

            try 
            {
                if (!suppressStopReport && _player != null && !_isFinished && !_completedForCurrentItem && !string.IsNullOrWhiteSpace(_activeReportItemId)) {
                    long stopPositionTicks = _lastKnownPositionTicks > 0 ? _lastKnownPositionTicks : GetPlayPositionMs() * 10000;
                    var info = new PlaybackProgressInfo
                    {
                        ItemId = _activeReportItemId,
                        PlaySessionId = _activeReportPlaySessionId,
                        MediaSourceId = _activeReportMediaSourceId,
                        PlayMethod = _activeReportPlayMethod,
                        PositionTicks = stopPositionTicks,
                        EventName = "Stop"
                    };
                    _ = ReportPlaybackStoppedSafeAsync(info);
                }
            } catch {}
            ClearReportingContext();
            _currentMediaSource = null;
            _playSessionId = null;
            
            try
            {
                if (_player == null) return;
                try { _progressTimer?.Stop(); } catch { }
                try { _osdTimer?.Stop(); } catch { }
                try { _subtitleRenderTimer?.Stop(); } catch { }
                try { _subtitleHideTimer?.Stop(); } catch { }
                try { _player.PlaybackCompleted -= OnPlaybackCompleted; } catch { }
                try { _player.ErrorOccurred -= OnPlayerErrorOccurred; } catch { }
                try { _player.BufferingProgressChanged -= OnBufferingProgressChanged; } catch { }
                _player.SubtitleUpdated -= OnSubtitleUpdated;
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

        private void OnPlayerErrorOccurred(object sender, PlayerErrorOccurredEventArgs e)
        {
        }

        private void OnBufferingProgressChanged(object sender, BufferingProgressChangedEventArgs e)
        {
        }

        public void HandleKey(AppKey key)
        {
            if (key == AppKey.Unknown) return;
            switch (key)
            {
                case AppKey.MediaPlayPause:
                {
                    bool pausedNow = TogglePause();
                    if (_osdVisible || pausedNow) ShowOSD();
                    break;
                }
                case AppKey.MediaPlay:
                    if (_player != null && _player.State == PlayerState.Paused)
                    {
                        TogglePause();
                        if (_osdVisible) ShowOSD();
                    }
                    break;
                case AppKey.MediaPause:
                    if (_player != null && _player.State == PlayerState.Playing)
                    {
                        bool pausedNow = TogglePause();
                        if (_osdVisible || pausedNow) ShowOSD();
                    }
                    break;
                case AppKey.MediaStop: NavigationService.NavigateBack(); break;
                case AppKey.MediaNext: PlayNextEpisode(); break;
                case AppKey.MediaRewind:
                    if (_osdVisible) Scrub(-30);
                    else HandleHiddenDirectionalSeek(-1);
                    break;
                case AppKey.MediaFastForward:
                    if (_osdVisible) Scrub(30);
                    else HandleHiddenDirectionalSeek(1);
                    break;
                case AppKey.Enter:
                    if ((!_osdVisible && HandleSmartPopupEnter()) ||
                        (_osdVisible && _smartPopupFocused && HandleSmartPopupEnter()))
                        break;
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
                    else if (_isSeeking) _ = CommitSeekAsync();
                    else if (_osdVisible) { if (_osdFocusRow == 1) ActivateOsdButton(); else { TogglePause(); _osdTimer.Stop(); _osdTimer.Start(); } }
                    else
                    {
                        bool pausedNow = TogglePause();
                        if (pausedNow) ShowOSD();
                    }
                    break;
                case AppKey.Left:
                    if (_audioOverlayVisible)
                    {
                        // Let FocusManager handle audio overlay navigation.
                    }
                    else if (_osdVisible && _smartPopupFocused)
                    {
                        // Keep popup focused; no lateral action.
                    }
                    else if (_subtitleOverlayVisible && _subtitleOffsetAdjustMode) AdjustSubtitleOffset(-SubtitleOffsetStepMs);
                    else if (_subtitleOverlayVisible) MoveSubtitleSelection(-1);
                    else if (_osdVisible && _osdFocusRow == 1) MoveButtonFocus(-1);
                    else if (_osdVisible) Scrub(-30);
                    else HandleHiddenDirectionalSeek(-1);
                    break;
                case AppKey.Right:
                    if (_audioOverlayVisible)
                    {
                        // Let FocusManager handle audio overlay navigation.
                    }
                    else if (_osdVisible && _smartPopupFocused)
                    {
                        // Keep popup focused; no lateral action.
                    }
                    else if (_subtitleOverlayVisible && _subtitleOffsetAdjustMode) AdjustSubtitleOffset(SubtitleOffsetStepMs);
                    else if (_subtitleOverlayVisible) MoveSubtitleSelection(1);
                    else if (_osdVisible && _osdFocusRow == 1) MoveButtonFocus(1);
                    else if (_osdVisible) Scrub(30);
                    else HandleHiddenDirectionalSeek(1);
                    break;
                case AppKey.Up:
                    if (_audioOverlayVisible)
                    {
                        MoveAudioSelection(-1);
                    }
                    else if (_subtitleOverlayVisible) 
                    {
                        if (_subtitleOffsetAdjustMode) ExitSubtitleOffsetAdjustMode();
                        else if (_subtitleIndex == 0)
                        {
                            ToggleSubtitleOffsetAdjustMode();
                        }
                        else MoveSubtitleSelection(-1);
                    }
                    else if (_osdVisible && _osdFocusRow == 0 && _smartPopupVisible && !_smartPopupFocused)
                    {
                        SetSmartPopupFocus(true);
                    }
                    else if (_osdVisible) MoveOsdRow(-1);
                    else ShowOSD();
                    break;
                case AppKey.Down:
                    if (_audioOverlayVisible)
                    {
                        MoveAudioSelection(1);
                    }
                    else if (_subtitleOverlayVisible) 
                    {
                        if (_subtitleOffsetAdjustMode) ExitSubtitleOffsetAdjustMode();
                        else MoveSubtitleSelection(1);
                    }
                    else if (_osdVisible && _smartPopupFocused)
                    {
                        SetSmartPopupFocus(false);
                    }
                    else if (_osdVisible) MoveOsdRow(1);
                    else ShowOSD();
                    break;
                case AppKey.Back:
                    if (_subtitleOverlayVisible) 
                    {
                        if (_subtitleOffsetAdjustMode) ExitSubtitleOffsetAdjustMode();
                        else HideSubtitleOverlay();
                    }
                    else if (_audioOverlayVisible) HideAudioOverlay();
                    else if (_subtitleOffsetAdjustMode) ExitSubtitleOffsetAdjustMode();
                    else if (!_osdVisible && _smartPopupVisible)
                    {
                        if (_isOutroPopupActive)
                            _autoNextCancelledByBack = true;
                        _smartPopupDismissedWhileHidden = true;
                        HideSmartPopup();
                    }
                    else if (_osdVisible && _smartPopupFocused)
                    {
                        SetSmartPopupFocus(false);
                    }
                    else if (_isSeeking)
                    {
                        CancelQueuedDirectionalSeek();
                        _isSeeking = false;
                        HideOSD();
                    }
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
                case AudioButtonIndex:
                    ShowAudioOverlay();
                    break;
                case SubtitleButtonIndex:
                    ShowSubtitleOverlay();
                    break;
                case NextButtonIndex:
                    if (_movie.ItemType == "Episode") PlayNextEpisode();
                    break;
            }
        }
        
        private async void PlayNextEpisode()
        {
            if (_isEpisodeSwitchInProgress)
                return;

            _autoNextTriggered = true;
            _autoNextCancelledByBack = false;
            _nextEpisodeCountdownMs = 0;
            HideSmartPopup();
            await SwitchEpisode(1);
        }

        private async Task SwitchEpisode(int offset)
        {
            if (_movie.ItemType != "Episode" || _isEpisodeSwitchInProgress) return;
            try
            {
                _isEpisodeSwitchInProgress = true;
                if (!_completedForCurrentItem)
                {
                    int durationMs = GetDuration();
                    int positionMs = GetPlayPositionMs();
                    if (durationMs > 0 && positionMs > 0 && ((double)positionMs / durationMs) > 0.95)
                        FinalizeCurrentItemAsPlayed();
                }

                _suppressStopReportOnce = true;
                StopPlayback();
                var seasons = await AppState.Jellyfin.GetSeasonsAsync(_movie.SeriesId);
                if (seasons == null || seasons.Count == 0) { NavigationService.NavigateBack(); return; }

                seasons = seasons
                    .OrderBy(s => s.IndexNumber)
                    .ThenBy(s => s.Name)
                    .ToList();

                var orderedEpisodes = new List<JellyfinMovie>();
                foreach (var season in seasons)
                {
                    var eps = await AppState.Jellyfin.GetEpisodesAsync(season.Id) ?? new List<JellyfinMovie>();
                    var orderedSeasonEpisodes = eps
                        .Where(e => e != null && e.ItemType == "Episode")
                        .OrderBy(e => e.IndexNumber)
                        .ThenBy(e => e.Name)
                        .ToList();
                    orderedEpisodes.AddRange(orderedSeasonEpisodes);
                }

                if (orderedEpisodes.Count == 0) { NavigationService.NavigateBack(); return; }

                int currentPos = orderedEpisodes.FindIndex(e => e.Id == _movie.Id);
                if (currentPos < 0)
                {
                    currentPos = orderedEpisodes.FindIndex(e =>
                        e.ParentIndexNumber == _movie.ParentIndexNumber &&
                        e.IndexNumber == _movie.IndexNumber);
                }

                JellyfinMovie target = null;
                int currentSeason = currentPos >= 0 ? orderedEpisodes[currentPos].ParentIndexNumber : _movie.ParentIndexNumber;
                int currentEpisodeNumber = currentPos >= 0 ? orderedEpisodes[currentPos].IndexNumber : _movie.IndexNumber;

                bool currentHasValidEpisodeNumber = currentSeason > 0 || currentEpisodeNumber > 0;
                var logicalTimeline = currentHasValidEpisodeNumber
                    ? orderedEpisodes.Where(e =>
                        e.Id == _movie.Id ||
                        e.ParentIndexNumber != currentSeason ||
                        e.IndexNumber != currentEpisodeNumber).ToList()
                    : orderedEpisodes;

                int logicalCurrentPos = logicalTimeline.FindIndex(e => e.Id == _movie.Id);
                if (logicalCurrentPos >= 0)
                {
                    int nextPos = logicalCurrentPos + (offset >= 0 ? 1 : -1);
                    if (nextPos >= 0 && nextPos < logicalTimeline.Count)
                        target = logicalTimeline[nextPos];
                }

                if (target == null)
                {
                    bool IsAfterCurrent(JellyfinMovie e) =>
                        e.ParentIndexNumber > currentSeason ||
                        (e.ParentIndexNumber == currentSeason && e.IndexNumber > currentEpisodeNumber);

                    bool IsBeforeCurrent(JellyfinMovie e) =>
                        e.ParentIndexNumber < currentSeason ||
                        (e.ParentIndexNumber == currentSeason && e.IndexNumber < currentEpisodeNumber);

                    target = offset > 0
                        ? logicalTimeline.FirstOrDefault(e => e.Id != _movie.Id && IsAfterCurrent(e))
                        : logicalTimeline.LastOrDefault(e => e.Id != _movie.Id && IsBeforeCurrent(e));
                }

                if (target == null || target.Id == _movie.Id)
                {
                    NavigationService.NavigateBack();
                    return;
                }

                LoadNewMedia(target);
            }
            catch (Exception) {NavigationService.NavigateBack(); }
            finally { _isEpisodeSwitchInProgress = false; }
        }

        private void LoadNewMedia(JellyfinMovie newMovie)
        {
            _movie = newMovie;
            RefreshTopOsdTitleView();
            _isFinished = false;
            _completedForCurrentItem = false;
            _isEpisodeSwitchInProgress = false;
            _isSeeking = false;
            _seekPreviewMs = 0;
            _startPositionMs = newMovie.PlaybackPositionTicks > 0 ? (int)(newMovie.PlaybackPositionTicks / 10000) : 0;
            _initialSeekDone = false;
            _progressFill.WidthSpecification = 0;
            _currentTimeLabel.Text = "00:00"; _durationLabel.Text = "-00:00";
            if (_clockLabel != null) _clockLabel.Text = FormatClockTime(DateTime.Now);
            if (_endsAtLabel != null) _endsAtLabel.Text = string.Empty;
            _overrideAudioIndex = null;
            _useParsedSubtitleRenderer = false;
            _subtitleCues.Clear();
            _activeSubtitleCueIndex = -1;
            StopSubtitleRenderTimer();
            ResetTrickplayState();
            ResetSmartActionState();
            StartPlayback();
            ShowOSD();
        }

        private bool TogglePause()
        {
            if (_player == null) return false;
            if (_player.State == PlayerState.Playing)
            {
                _player.Pause();
                ShowPlayPauseFeedback(isPaused: true);
                ReportProgressToServer(force: true);
                return true;
            }
            else if (_player.State == PlayerState.Paused)
            {
                _player.Start();
                ShowPlayPauseFeedback(isPaused: false);
                ReportProgressToServer(force: true);
                return false;
            }
            return false;
        }
    }
}
