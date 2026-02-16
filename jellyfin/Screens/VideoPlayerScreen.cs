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
        private ImageView _subtitleImage;
        private TextLabel _subtitleText;
        private Timer _subtitleHideTimer;
        private Timer _subtitleRenderTimer;
        private List<SubtitleCue> _subtitleCues = new List<SubtitleCue>();
        private bool _useParsedSubtitleRenderer;
        private int _activeSubtitleCueIndex = -1;
        private Timer _reportProgressTimer;
        private bool _isFinished;
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
        private const int PgsHideGraceMs = 0;

        // --- NEW: Store MediaSource and Override Audio ---
        private MediaSourceInfo _currentMediaSource;
        private int? _overrideAudioIndex = null;
        private bool _isAnamorphicVideo;
        private bool _isPgs;
        private List<PgsCue> _pgsCues = new List<PgsCue>();
        private int _pgsRenderToken;
        private string _lastPgsCachePath;
        private int _pgsHideGraceUntilMs;
        private readonly Dictionary<int, PgsCachedCue> _pgsCueCache = new();

        private struct PgsCue
        {
            public long StartMs;
            public long EndMs;
            public long FileOffset;
        }

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
            _trickplayCacheDir = System.IO.Path.Combine(Application.Current.DirectoryInfo.Data, "trickplay-cache");

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
            UiAnimator.StopAndDispose(ref _osdAnimation);
            UiAnimator.StopAndDispose(ref _topOsdAnimation);
            UiAnimator.StopAndDispose(ref _subtitleOverlayAnimation);
            UiAnimator.StopAndDispose(ref _audioOverlayAnimation);
            UiAnimator.StopAndDispose(ref _subtitleTextAnimation);
            UiAnimator.StopAndDispose(ref _seekFeedbackAnimation);
            UiAnimator.StopAndDisposeAll(_focusAnimations);
            _seekCommitTimer?.Stop();
            _seekCommitTimer = null;
            StopPlayback();
            Window.Default.BackgroundColor = Color.Black;
            BackgroundColor = Color.Black;
        }

        private async void StartPlayback()
        {
            try
            {
                _suppressPlaybackCompletedNavigation = false;
                _subtitleEnabled = _initialSubtitleIndex.HasValue;
                _subtitleOffsetBurnInWarningShown = false;
                _useParsedSubtitleRenderer = false;
                _isPgs = false;
                _subtitleCues.Clear();
                _pgsCues.Clear();
                ClearPgsCueCache();
                _pgsRenderToken = 0;
                _pgsHideGraceUntilMs = 0;
                _activeSubtitleCueIndex = -1;
                _player = new Player();

                _player.ErrorOccurred += OnPlayerErrorOccurred;
                _player.BufferingProgressChanged += OnBufferingProgressChanged;
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
                string codec = (_codec ?? string.Empty).ToLowerInvariant();
                string fallbackExt = "srt";
                var formatCandidates = new List<string>();

                if (codec.Contains("pgs"))
                {
                    fallbackExt = "sup";
                    formatCandidates.Add("sup");
                    formatCandidates.Add("pgssub");
                    formatCandidates.Add("pgs");
                }
                else if (codec.Contains("ass") || codec.Contains("ssa"))
                {
                    fallbackExt = "ssa";
                    formatCandidates.Add("ssa");
                    formatCandidates.Add("ass");
                }
                else if (codec.Contains("vtt"))
                {
                    fallbackExt = "vtt";
                    formatCandidates.Add("vtt");
                }
                else
                {
                    formatCandidates.Add("srt");
                    formatCandidates.Add("subrip");
                }

                var subtitleStream = _currentMediaSource?.MediaStreams?
                    .FirstOrDefault(s => s.Type == "Subtitle" && s.Index == subtitleIndex);

                var apiKey = AppState.AccessToken ?? string.Empty;
                var serverUrl = (AppState.Jellyfin.ServerUrl ?? string.Empty).TrimEnd('/');
                var downloadCandidates = new List<string>();

                // Preferred: Jellyfin-provided subtitle delivery URL.
                if (!string.IsNullOrWhiteSpace(subtitleStream?.DeliveryUrl))
                {
                    string deliveryUrl = subtitleStream.DeliveryUrl;
                    if (deliveryUrl.StartsWith("/"))
                        deliveryUrl = $"{serverUrl}{deliveryUrl}";
                    if (!deliveryUrl.Contains("api_key=") && !deliveryUrl.Contains("Token="))
                        deliveryUrl += (deliveryUrl.Contains("?") ? "&" : "?") + $"api_key={Uri.EscapeDataString(apiKey)}";
                    downloadCandidates.Add(deliveryUrl);
                }

                foreach (var format in formatCandidates.Distinct())
                {
                    downloadCandidates.Add($"{serverUrl}/Videos/{_movie.Id}/{mediaSourceId}/Subtitles/{subtitleIndex}/0/Stream.{format}?api_key={Uri.EscapeDataString(apiKey)}");
                    downloadCandidates.Add($"{serverUrl}/Videos/{_movie.Id}/{mediaSourceId}/Subtitles/{subtitleIndex}/Stream.{format}?api_key={Uri.EscapeDataString(apiKey)}");
                }

                string downloadedExt = fallbackExt;
                string localPath = null;

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("X-Emby-Authorization", $"MediaBrowser Client=\"JellyfinTizen\", Device=\"SamsungTV\", DeviceId=\"tizen-tv\", Version=\"1.0\", Token=\"{apiKey}\"");

                    foreach (var url in downloadCandidates.Distinct())
                    {
                        try
                        {
                            var response = await client.GetAsync(url);
                            if (!response.IsSuccessStatusCode)
                            {
                                Console.WriteLine($"[Subtitle] Download failed {(int)response.StatusCode} for {url}");
                                continue;
                            }

                            downloadedExt = TryGetSubtitleExtensionFromUrl(url, fallbackExt);
                            localPath = System.IO.Path.Combine(Application.Current.DirectoryInfo.Data, $"sub_{mediaSourceId}_{subtitleIndex}.{downloadedExt}");
                            var data = await response.Content.ReadAsByteArrayAsync();
                            await File.WriteAllBytesAsync(localPath, data);
                            break;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Subtitle] Download exception for {url}: {ex.Message}");
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(localPath))
                    throw new HttpRequestException("Subtitle download failed for all candidate URLs.");

                _externalSubtitlePath = localPath;

                bool isPgsCodec = codec.Contains("pgs");
                bool pgsLikeExt = downloadedExt == "sup" || downloadedExt == "pgssub" || downloadedExt == "pgs";
                if ((isPgsCodec || pgsLikeExt) && TryLoadPgsSubtitleCues(localPath))
                {
                    _useParsedSubtitleRenderer = true;
                    _isPgs = true;
                    TryDisableNativeSubtitleTrack();
                    StartSubtitleRenderTimer();
                }
                else if (isPgsCodec || pgsLikeExt)
                {
                    // Do not fall back to text parser for PGS tracks.
                    _useParsedSubtitleRenderer = false;
                    _isPgs = false;
                    _subtitleCues.Clear();
                    _pgsCues.Clear();
                    StopSubtitleRenderTimer();
                    return false;
                }
                else if (TryLoadSrtSubtitleCues(localPath))
                {
                    _useParsedSubtitleRenderer = true;
                    _isPgs = false;
                    TryDisableNativeSubtitleTrack();
                    StartSubtitleRenderTimer();
                }
                else
                {
                    _useParsedSubtitleRenderer = false;
                    _isPgs = false;
                    _player.SetSubtitle(localPath);
                }

                return true;
            }
            catch
            {
                _externalSubtitlePath = null;
                _useParsedSubtitleRenderer = false;
                _isPgs = false;
                _subtitleCues.Clear();
                _pgsCues.Clear();
                ClearPgsCueCache();
                _pgsRenderToken = 0;
                _pgsHideGraceUntilMs = 0;
                StopSubtitleRenderTimer();
                return false;
            }
        }

        private static string TryGetSubtitleExtensionFromUrl(string url, string fallbackExt)
        {
            if (string.IsNullOrWhiteSpace(url)) return fallbackExt;
            string cleanUrl = url.Split('?')[0];
            string ext = System.IO.Path.GetExtension(cleanUrl).TrimStart('.').ToLowerInvariant();
            return string.IsNullOrWhiteSpace(ext) ? fallbackExt : ext;
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

        private bool TryLoadPgsSubtitleCues(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;
                var cues = new List<PgsCue>();
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (var br = new BinaryReader(fs))
                {
                    long length = fs.Length;
                    PgsCue currentCue = new PgsCue { StartMs = -1, EndMs = -1, FileOffset = -1 };

                    while (fs.Position < length - 13)
                    {
                        long segmentOffset = fs.Position;
                        byte b0 = br.ReadByte();
                        byte b1 = br.ReadByte();
                        if (b0 != 0x50 || b1 != 0x47)
                        {
                            // Re-sync safely if parser drifts into payload bytes.
                            fs.Seek(segmentOffset + 1, SeekOrigin.Begin);
                            continue;
                        }

                        uint pts = (uint)((br.ReadByte() << 24) | (br.ReadByte() << 16) | (br.ReadByte() << 8) | br.ReadByte());
                        fs.Seek(4, SeekOrigin.Current); // skip DTS
                        byte type = br.ReadByte();
                        ushort size = (ushort)((br.ReadByte() << 8) | br.ReadByte());
                        long payloadEnd = fs.Position + size;
                        if (payloadEnd > length)
                            break;

                        if (type == 0x16) // PCS: new display set start
                        {
                            long ms = pts / 90;
                            bool hasObjects = true;

                            if (size >= 11)
                            {
                                // video size + frame rate + composition number + state + palette update flag + palette id
                                fs.Seek(2 + 2 + 1 + 2 + 1 + 1 + 1, SeekOrigin.Current);
                                hasObjects = fs.Position < payloadEnd && br.ReadByte() > 0;
                            }

                            if (currentCue.StartMs >= 0)
                            {
                                currentCue.EndMs = ms;
                                if (currentCue.EndMs > currentCue.StartMs)
                                    cues.Add(currentCue);
                            }

                            if (hasObjects)
                            {
                                currentCue = new PgsCue
                                {
                                    StartMs = ms,
                                    EndMs = -1,
                                    FileOffset = segmentOffset
                                };
                            }
                            else
                            {
                                // Clear event (hide subtitle), no active cue.
                                currentCue = new PgsCue { StartMs = -1, EndMs = -1, FileOffset = -1 };
                            }
                        }

                        fs.Seek(payloadEnd, SeekOrigin.Begin);
                    }

                    if (currentCue.StartMs >= 0)
                    {
                        currentCue.EndMs = currentCue.StartMs + 1000;
                        cues.Add(currentCue);
                    }
                }

                _pgsCues = cues;
                ClearPgsCueCache();
                _pgsRenderToken = 0;
                _pgsHideGraceUntilMs = 0;
                _activeSubtitleCueIndex = -1;
                return cues.Count > 0;
            }
            catch
            {
                _pgsCues.Clear();
                ClearPgsCueCache();
                _pgsRenderToken = 0;
                _pgsHideGraceUntilMs = 0;
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
            if (!_useParsedSubtitleRenderer || _player == null || !_subtitleEnabled)
            {
                _subtitleText?.Hide();
                _subtitleImage?.Hide();
                _activeSubtitleCueIndex = -1;
                return;
            }

            int queryPosMs = GetPlayPositionMs() - _subtitleOffsetMs;
            if (queryPosMs < 0)
            {
                _subtitleText?.Hide();
                _subtitleImage?.Hide();
                _activeSubtitleCueIndex = -1;
                return;
            }

            if (_isPgs)
            {
                _subtitleText?.Hide();
                UpdatePgsRender(queryPosMs);
                return;
            }

            if (_subtitleText == null || _subtitleCues.Count == 0)
            {
                _subtitleText?.Hide();
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

        private void UpdatePgsRender(int queryPosMs)
        {
            if (_pgsCues.Count == 0)
            {
                _subtitleImage?.Hide();
                _activeSubtitleCueIndex = -1;
                return;
            }

            int cueIndex = -1;
            for (int i = 0; i < _pgsCues.Count; i++)
            {
                if (queryPosMs >= _pgsCues[i].StartMs && queryPosMs < _pgsCues[i].EndMs)
                {
                    cueIndex = i;
                    break;
                }
            }

            if (cueIndex == -1)
            {
                if (queryPosMs < _pgsHideGraceUntilMs)
                    return;
                if (_activeSubtitleCueIndex == -1) return;
                _activeSubtitleCueIndex = -1;
                _pgsRenderToken++;
                _subtitleImage?.Hide();
                return;
            }

            if (cueIndex == _activeSubtitleCueIndex) return;
            _activeSubtitleCueIndex = cueIndex;
            _pgsHideGraceUntilMs = (int)Math.Min(int.MaxValue, _pgsCues[cueIndex].EndMs + PgsHideGraceMs);
            int renderToken = ++_pgsRenderToken;
            _ = RenderPgsCueAsync(_pgsCues[cueIndex], renderToken);
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

        private sealed class PgsRenderFrame
        {
            public int PlaneWidth;
            public int PlaneHeight;
            public int ObjectX;
            public int ObjectY;
            public int Width;
            public int Height;
            public byte[] BgraPixels;
        }

        private sealed class PgsCompositionObject
        {
            public int ObjectId;
            public int WindowId;
            public int X;
            public int Y;
            public bool HasCrop;
            public int CropX;
            public int CropY;
            public int CropWidth;
            public int CropHeight;
        }

        private sealed class PgsWindow
        {
            public int X;
            public int Y;
            public int Width;
            public int Height;
        }

        private sealed class PgsObjectData
        {
            public int Width;
            public int Height;
            public int ExpectedPayloadBytes = -1;
            public bool IsComplete;
            public readonly List<byte> Data = new List<byte>();
        }

        private sealed class PgsCachedCue
        {
            public string CachePath;
            public int PlaneWidth;
            public int PlaneHeight;
            public int ObjectX;
            public int ObjectY;
            public int Width;
            public int Height;
        }

        private void ClearPgsCueCache()
        {
            foreach (var entry in _pgsCueCache.Values)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(entry.CachePath) && File.Exists(entry.CachePath))
                        File.Delete(entry.CachePath);
                }
                catch { }
            }
            _pgsCueCache.Clear();
        }

        private async Task RenderPgsCueAsync(PgsCue cue, int renderToken)
        {
            if (string.IsNullOrWhiteSpace(_externalSubtitlePath))
                return;

            try
            {
                int cueIndex = _activeSubtitleCueIndex;
                if (cueIndex >= 0 && _pgsCueCache.TryGetValue(cueIndex, out var cached) && !string.IsNullOrWhiteSpace(cached.CachePath) && File.Exists(cached.CachePath))
                {
                    RunOnUiThread(() =>
                    {
                        if (renderToken != _pgsRenderToken) return;
                        if (_subtitleImage == null) return;
                        ApplyRenderedPgsCue(cached.CachePath, cached.PlaneWidth, cached.PlaneHeight, cached.ObjectX, cached.ObjectY, cached.Width, cached.Height);
                    });
                    return;
                }

                var frame = await Task.Run(() => ParsePgsDisplaySet(_externalSubtitlePath, cue.FileOffset));
                if (frame == null || frame.Width <= 0 || frame.Height <= 0 || frame.BgraPixels == null || frame.BgraPixels.Length == 0)
                {
                    RunOnUiThread(() =>
                    {
                        if (renderToken != _pgsRenderToken) return;
                        _subtitleImage?.Hide();
                    });
                    return;
                }

                string cachePath = System.IO.Path.Combine(Application.Current.DirectoryInfo.Data, $"pgs_cache_{renderToken}_{Math.Max(0, cueIndex)}.bmp");
                await Task.Run(() => WritePgsBitmap(cachePath, frame));

                RunOnUiThread(() =>
                {
                    if (renderToken != _pgsRenderToken) return;
                    if (_subtitleImage == null) return;
                    ApplyRenderedPgsCue(cachePath, frame.PlaneWidth, frame.PlaneHeight, frame.ObjectX, frame.ObjectY, frame.Width, frame.Height);

                    try
                    {
                        if (!string.IsNullOrWhiteSpace(_lastPgsCachePath) && _lastPgsCachePath != cachePath && File.Exists(_lastPgsCachePath))
                            File.Delete(_lastPgsCachePath);
                    }
                    catch { }
                    _lastPgsCachePath = cachePath;

                    if (cueIndex >= 0)
                    {
                        _pgsCueCache[cueIndex] = new PgsCachedCue
                        {
                            CachePath = cachePath,
                            PlaneWidth = frame.PlaneWidth,
                            PlaneHeight = frame.PlaneHeight,
                            ObjectX = frame.ObjectX,
                            ObjectY = frame.ObjectY,
                            Width = frame.Width,
                            Height = frame.Height
                        };
                    }
                });
            }
            catch
            {
                RunOnUiThread(() =>
                {
                    if (renderToken != _pgsRenderToken) return;
                    _subtitleImage?.Hide();
                });
            }
        }

        private void ApplyRenderedPgsCue(string cachePath, int planeW, int planeH, int objectX, int objectY, int width, int height)
        {
            if (_subtitleImage == null) return;

            int screenW = Window.Default.Size.Width;
            int screenH = Window.Default.Size.Height;
            int pW = planeW > 0 ? planeW : screenW;
            int pH = planeH > 0 ? planeH : screenH;

            float sx = (float)screenW / Math.Max(1, pW);
            float sy = (float)screenH / Math.Max(1, pH);

            int drawW = Math.Max(1, (int)Math.Round(width * sx));
            int drawH = Math.Max(1, (int)Math.Round(height * sy));
            int drawX = (int)Math.Round(objectX * sx);
            int drawY = (int)Math.Round(objectY * sy);

            drawX = Math.Clamp(drawX, 0, Math.Max(0, screenW - drawW));
            drawY = Math.Clamp(drawY, 0, Math.Max(0, screenH - drawH));

            _subtitleImage.WidthSpecification = drawW;
            _subtitleImage.HeightSpecification = drawH;
            _subtitleImage.PositionX = drawX;
            _subtitleImage.PositionY = drawY;
            _subtitleImage.ResourceUrl = cachePath;
            _subtitleImage.Show();
        }

        private static PgsRenderFrame ParsePgsDisplaySet(string path, long cueOffset)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            if (cueOffset < 0 || cueOffset >= fs.Length - 13)
                return null;

            int planeW = 0;
            int planeH = 0;
            byte[] palette = null;
            var compObjects = new List<PgsCompositionObject>();
            var windows = new Dictionary<int, PgsWindow>();
            var objectMap = new Dictionary<int, PgsObjectData>();

            // PGS epochs can carry palette/objects across display sets.
            LoadPgsEpochStateUpToOffset(br, fs, cueOffset, objectMap, ref palette);

            fs.Seek(cueOffset, SeekOrigin.Begin);
            while (fs.Position <= fs.Length - 13)
            {
                long segStart = fs.Position;
                if (br.ReadByte() != 0x50 || br.ReadByte() != 0x47)
                    break;

                fs.Seek(8, SeekOrigin.Current);
                byte type = br.ReadByte();
                ushort size = (ushort)((br.ReadByte() << 8) | br.ReadByte());
                long payloadEnd = fs.Position + size;
                if (payloadEnd > fs.Length)
                    break;

                if (type == 0x16 && size >= 11)
                {
                    planeW = (br.ReadByte() << 8) | br.ReadByte();
                    planeH = (br.ReadByte() << 8) | br.ReadByte();
                    br.ReadByte(); // frame rate
                    fs.Seek(2, SeekOrigin.Current); // composition number
                    int state = br.ReadByte() >> 6;
                    br.ReadByte(); // palette update flag
                    br.ReadByte(); // palette id
                    byte objectCount = br.ReadByte();
                    compObjects.Clear();
                    windows.Clear();

                    if (state != 0)
                    {
                        objectMap.Clear();
                        palette = null;
                    }

                    for (int i = 0; i < objectCount && fs.Position + 8 <= payloadEnd; i++)
                    {
                        int objectId = (br.ReadByte() << 8) | br.ReadByte();
                        int windowId = br.ReadByte();
                        byte compositionFlag = br.ReadByte();
                        int objX = (br.ReadByte() << 8) | br.ReadByte();
                        int objY = (br.ReadByte() << 8) | br.ReadByte();

                        bool hasCrop = (compositionFlag & 0x80) != 0;
                        int cropX = 0, cropY = 0, cropW = 0, cropH = 0;
                        if (hasCrop && fs.Position + 8 <= payloadEnd)
                        {
                            cropX = (br.ReadByte() << 8) | br.ReadByte();
                            cropY = (br.ReadByte() << 8) | br.ReadByte();
                            cropW = (br.ReadByte() << 8) | br.ReadByte();
                            cropH = (br.ReadByte() << 8) | br.ReadByte();
                        }

                        compObjects.Add(new PgsCompositionObject
                        {
                            ObjectId = objectId,
                            WindowId = windowId,
                            X = objX,
                            Y = objY,
                            HasCrop = hasCrop,
                            CropX = cropX,
                            CropY = cropY,
                            CropWidth = cropW,
                            CropHeight = cropH
                        });
                    }
                }
                else if (type == 0x17)
                {
                    ParsePgsWindowSegment(br, fs, payloadEnd, windows);
                }
                else if (type == 0x14)
                {
                    ParsePgsPaletteSegment(br, fs, payloadEnd, ref palette);
                }
                else if (type == 0x15)
                {
                    ParsePgsObjectSegment(br, fs, payloadEnd, objectMap);
                }
                else if (type == 0x80)
                {
                    if (segStart >= cueOffset && palette != null && compObjects.Count > 0)
                        break;
                }

                fs.Seek(payloadEnd, SeekOrigin.Begin);
            }

            if (palette == null || compObjects.Count == 0)
                return null;

            int canvasW = planeW > 0 ? planeW : 1920;
            int canvasH = planeH > 0 ? planeH : 1080;
            var canvas = new byte[canvasW * canvasH * 4];

            foreach (var comp in compObjects)
            {
                if (!objectMap.TryGetValue(comp.ObjectId, out var obj) || obj.Width <= 0 || obj.Height <= 0 || obj.Data.Count == 0)
                    continue;

                byte[] rle = obj.Data.ToArray();
                if (obj.ExpectedPayloadBytes > 0 && rle.Length > obj.ExpectedPayloadBytes)
                    rle = rle.Take(obj.ExpectedPayloadBytes).ToArray();

                var decoded = DecodePgsRleToBgra(rle, obj.Width, obj.Height, palette);
                RemoveLikelyBlackBackdrop(decoded, obj.Width, obj.Height);
                int srcX = 0;
                int srcY = 0;
                int drawW = obj.Width;
                int drawH = obj.Height;
                int dstX = comp.X;
                int dstY = comp.Y;

                if (comp.HasCrop && comp.CropWidth > 0 && comp.CropHeight > 0)
                {
                    srcX = Math.Clamp(comp.CropX, 0, Math.Max(0, obj.Width - 1));
                    srcY = Math.Clamp(comp.CropY, 0, Math.Max(0, obj.Height - 1));
                    drawW = Math.Min(comp.CropWidth, obj.Width - srcX);
                    drawH = Math.Min(comp.CropHeight, obj.Height - srcY);
                }

                if (drawW <= 0 || drawH <= 0)
                    continue;

                if (windows.TryGetValue(comp.WindowId, out var win))
                {
                    int leftClip = Math.Max(0, win.X - dstX);
                    int topClip = Math.Max(0, win.Y - dstY);
                    int rightClip = Math.Max(0, (dstX + drawW) - (win.X + win.Width));
                    int bottomClip = Math.Max(0, (dstY + drawH) - (win.Y + win.Height));

                    srcX += leftClip;
                    srcY += topClip;
                    dstX += leftClip;
                    dstY += topClip;
                    drawW -= (leftClip + rightClip);
                    drawH -= (topClip + bottomClip);
                }

                if (drawW <= 0 || drawH <= 0)
                    continue;

                BlitPremultipliedBgraRegion(
                    canvas, canvasW, canvasH,
                    decoded, obj.Width, obj.Height,
                    srcX, srcY, drawW, drawH,
                    dstX, dstY
                );
            }

            if (!TryFindOpaqueBounds(canvas, canvasW, canvasH, out int minX, out int minY, out int maxX, out int maxY))
                return null;

            int outW = maxX - minX + 1;
            int outH = maxY - minY + 1;
            var cropped = new byte[outW * outH * 4];
            for (int y = 0; y < outH; y++)
            {
                int src = ((minY + y) * canvasW + minX) * 4;
                int dst = y * outW * 4;
                Buffer.BlockCopy(canvas, src, cropped, dst, outW * 4);
            }

            return new PgsRenderFrame
            {
                PlaneWidth = canvasW,
                PlaneHeight = canvasH,
                ObjectX = minX,
                ObjectY = minY,
                Width = outW,
                Height = outH,
                BgraPixels = cropped
            };
        }

        private static void LoadPgsEpochStateUpToOffset(BinaryReader br, FileStream fs, long offset, Dictionary<int, PgsObjectData> objectMap, ref byte[] palette)
        {
            fs.Seek(0, SeekOrigin.Begin);
            while (fs.Position <= fs.Length - 13 && fs.Position < offset)
            {
                long segStart = fs.Position;
                if (br.ReadByte() != 0x50 || br.ReadByte() != 0x47)
                {
                    fs.Seek(segStart + 1, SeekOrigin.Begin);
                    continue;
                }

                fs.Seek(8, SeekOrigin.Current);
                byte type = br.ReadByte();
                ushort size = (ushort)((br.ReadByte() << 8) | br.ReadByte());
                long payloadEnd = fs.Position + size;
                if (payloadEnd > fs.Length)
                    break;

                if (type == 0x16 && size >= 7)
                {
                    fs.Seek(2 + 2 + 1 + 2, SeekOrigin.Current); // width/height/frameRate/compositionNumber
                    int state = br.ReadByte() >> 6;
                    if (state != 0)
                    {
                        objectMap.Clear();
                        palette = null;
                    }
                }
                else if (type == 0x14)
                {
                    ParsePgsPaletteSegment(br, fs, payloadEnd, ref palette);
                }
                else if (type == 0x15)
                {
                    ParsePgsObjectSegment(br, fs, payloadEnd, objectMap);
                }

                fs.Seek(payloadEnd, SeekOrigin.Begin);
            }
        }

        private static void ParsePgsWindowSegment(BinaryReader br, FileStream fs, long payloadEnd, Dictionary<int, PgsWindow> windows)
        {
            if (fs.Position >= payloadEnd) return;
            int count = br.ReadByte();
            windows.Clear();
            for (int i = 0; i < count && fs.Position + 9 <= payloadEnd; i++)
            {
                int windowId = br.ReadByte();
                int x = (br.ReadByte() << 8) | br.ReadByte();
                int y = (br.ReadByte() << 8) | br.ReadByte();
                int w = (br.ReadByte() << 8) | br.ReadByte();
                int h = (br.ReadByte() << 8) | br.ReadByte();
                windows[windowId] = new PgsWindow { X = x, Y = y, Width = w, Height = h };
            }
        }

        private static void ParsePgsPaletteSegment(BinaryReader br, FileStream fs, long payloadEnd, ref byte[] palette)
        {
            if (palette == null)
                palette = new byte[256 * 4];

            if (fs.Position + 2 > payloadEnd) return;
            fs.Seek(2, SeekOrigin.Current); // palette id + version

            while (fs.Position + 5 <= payloadEnd)
            {
                int idx = br.ReadByte();
                int y = br.ReadByte();
                int cr = br.ReadByte();
                int cb = br.ReadByte();
                int a = br.ReadByte();

                int r = (int)(y + 1.402 * (cr - 128));
                int g = (int)(y - 0.34414 * (cb - 128) - 0.71414 * (cr - 128));
                int b = (int)(y + 1.772 * (cb - 128));

                palette[idx * 4 + 0] = (byte)Math.Clamp(b, 0, 255);
                palette[idx * 4 + 1] = (byte)Math.Clamp(g, 0, 255);
                palette[idx * 4 + 2] = (byte)Math.Clamp(r, 0, 255);
                palette[idx * 4 + 3] = (byte)a;
            }

            // Transparent index in PGS is conventionally 0.
            palette[3] = 0;
        }

        private static void ParsePgsObjectSegment(BinaryReader br, FileStream fs, long payloadEnd, Dictionary<int, PgsObjectData> objectMap)
        {
            if (fs.Position + 4 > payloadEnd) return;

            int objectId = (br.ReadByte() << 8) | br.ReadByte();
            br.ReadByte(); // object version
            byte sequenceFlag = br.ReadByte();

            if (!objectMap.TryGetValue(objectId, out var obj))
            {
                obj = new PgsObjectData();
                objectMap[objectId] = obj;
            }

            if ((sequenceFlag & 0x80) != 0)
            {
                if (fs.Position + 7 > payloadEnd) return;
                obj.ExpectedPayloadBytes = (br.ReadByte() << 16) | (br.ReadByte() << 8) | br.ReadByte();
                obj.Width = (br.ReadByte() << 8) | br.ReadByte();
                obj.Height = (br.ReadByte() << 8) | br.ReadByte();
                obj.Data.Clear();
                obj.IsComplete = false;
            }

            int remaining = (int)(payloadEnd - fs.Position);
            if (remaining > 0)
                obj.Data.AddRange(br.ReadBytes(remaining));

            if ((sequenceFlag & 0x40) != 0)
                obj.IsComplete = true;
        }

        private static byte[] DecodePgsRleToBgra(byte[] rle, int width, int height, byte[] palette)
        {
            var output = new byte[width * height * 4];
            int x = 0;
            int y = 0;
            int idx = 0;

            while (idx < rle.Length && y < height)
            {
                byte code = rle[idx++];
                if (code != 0)
                {
                    WriteRun(output, width, height, ref x, ref y, 1, code, palette);
                    continue;
                }

                if (idx >= rle.Length) break;
                byte flags = rle[idx++];
                if (flags == 0)
                {
                    x = 0;
                    y++;
                    continue;
                }

                int runLength;
                byte colorIndex = 0;

                if ((flags & 0xC0) == 0x00)
                {
                    runLength = flags & 0x3F;
                }
                else if ((flags & 0xC0) == 0x40)
                {
                    if (idx >= rle.Length) break;
                    runLength = ((flags & 0x3F) << 8) | rle[idx++];
                }
                else if ((flags & 0xC0) == 0x80)
                {
                    if (idx >= rle.Length) break;
                    runLength = flags & 0x3F;
                    colorIndex = rle[idx++];
                }
                else
                {
                    if (idx + 1 >= rle.Length) break;
                    runLength = ((flags & 0x3F) << 8) | rle[idx++];
                    colorIndex = rle[idx++];
                }

                if (runLength <= 0) continue;
                WriteRun(output, width, height, ref x, ref y, runLength, colorIndex, palette);
            }

            return output;
        }

        private static void RemoveLikelyBlackBackdrop(byte[] pixels, int width, int height)
        {
            if (pixels == null || pixels.Length < 4) return;

            int opaqueCount = 0;
            int opaqueBlackCount = 0;
            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte b = pixels[i + 0];
                byte g = pixels[i + 1];
                byte r = pixels[i + 2];
                byte a = pixels[i + 3];
                if (a < 180) continue;
                opaqueCount++;
                if (r < 26 && g < 26 && b < 26) opaqueBlackCount++;
            }

            // Only run stripping when the object is clearly dominated by black backing.
            if (opaqueCount == 0 || opaqueBlackCount * 100 < opaqueCount * 45)
                return;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    int p = i * 4;
                    byte b = pixels[p + 0];
                    byte g = pixels[p + 1];
                    byte r = pixels[p + 2];
                    byte a = pixels[p + 3];

                    if (a < 180) continue;

                    int max = Math.Max(r, Math.Max(g, b));
                    int min = Math.Min(r, Math.Min(g, b));
                    int sat = max - min;
                    int lum = (r * 77 + g * 150 + b * 29) >> 8;

                    // Aggressively remove opaque neutral-dark backing boxes.
                    if (lum < 72 && sat < 32)
                        pixels[p + 3] = 0;
                }
            }
        }

        private static void WriteRun(byte[] output, int width, int height, ref int x, ref int y, int runLength, byte colorIndex, byte[] palette)
        {
            int p = colorIndex * 4;
            byte b = palette[p + 0];
            byte g = palette[p + 1];
            byte r = palette[p + 2];
            byte a = palette[p + 3];

            for (int i = 0; i < runLength && y < height; i++)
            {
                if (x >= width)
                {
                    x = 0;
                    y++;
                    if (y >= height) break;
                }

                int dst = (y * width + x) * 4;
                output[dst + 0] = b;
                output[dst + 1] = g;
                output[dst + 2] = r;
                output[dst + 3] = a;
                x++;
            }
        }

        private static void BlitPremultipliedBgra(byte[] dst, int dstW, int dstH, byte[] src, int srcW, int srcH, int dstX, int dstY)
        {
            BlitPremultipliedBgraRegion(dst, dstW, dstH, src, srcW, srcH, 0, 0, srcW, srcH, dstX, dstY);
        }

        private static void BlitPremultipliedBgraRegion(
            byte[] dst, int dstW, int dstH,
            byte[] src, int srcW, int srcH,
            int srcX, int srcY, int copyW, int copyH,
            int dstX, int dstY)
        {
            if (copyW <= 0 || copyH <= 0) return;
            srcX = Math.Clamp(srcX, 0, Math.Max(0, srcW - 1));
            srcY = Math.Clamp(srcY, 0, Math.Max(0, srcH - 1));
            copyW = Math.Min(copyW, srcW - srcX);
            copyH = Math.Min(copyH, srcH - srcY);

            if (copyW <= 0 || copyH <= 0) return;

            for (int y = 0; y < srcH; y++)
            {
                if (y >= copyH) break;
                int sy = srcY + y;
                int ty = dstY + y;
                if (ty < 0 || ty >= dstH) continue;
                for (int x = 0; x < copyW; x++)
                {
                    int sx = srcX + x;
                    int tx = dstX + x;
                    if (tx < 0 || tx >= dstW) continue;

                    int s = (sy * srcW + sx) * 4;
                    byte sa = src[s + 3];
                    if (sa == 0) continue;

                    int d = (ty * dstW + tx) * 4;
                    if (sa == 255)
                    {
                        dst[d + 0] = src[s + 0];
                        dst[d + 1] = src[s + 1];
                        dst[d + 2] = src[s + 2];
                        dst[d + 3] = 255;
                        continue;
                    }

                    int invA = 255 - sa;
                    dst[d + 0] = (byte)((src[s + 0] * sa + dst[d + 0] * invA) / 255);
                    dst[d + 1] = (byte)((src[s + 1] * sa + dst[d + 1] * invA) / 255);
                    dst[d + 2] = (byte)((src[s + 2] * sa + dst[d + 2] * invA) / 255);
                    dst[d + 3] = (byte)Math.Clamp(sa + (dst[d + 3] * invA) / 255, 0, 255);
                }
            }
        }

        private static bool TryFindOpaqueBounds(byte[] pixels, int width, int height, out int minX, out int minY, out int maxX, out int maxY)
        {
            minX = width;
            minY = height;
            maxX = -1;
            maxY = -1;

            for (int y = 0; y < height; y++)
            {
                int row = y * width * 4;
                for (int x = 0; x < width; x++)
                {
                    byte a = pixels[row + x * 4 + 3];
                    if (a == 0) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            return maxX >= minX && maxY >= minY;
        }

        private static void WritePgsBitmap(string cachePath, PgsRenderFrame frame)
        {
            using var bmp = new FileStream(cachePath, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(bmp);

            int dataSize = frame.Width * frame.Height * 4;
            int fileSize = 54 + dataSize;

            bw.Write((byte)'B');
            bw.Write((byte)'M');
            bw.Write(fileSize);
            bw.Write(0);
            bw.Write(54);
            bw.Write(40);
            bw.Write(frame.Width);
            bw.Write(-frame.Height); // top-down
            bw.Write((short)1);
            bw.Write((short)32);
            bw.Write(0);
            bw.Write(dataSize);
            bw.Write(0);
            bw.Write(0);
            bw.Write(0);
            bw.Write(0);

            bw.Write(frame.BgraPixels);
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

            _topOsd.Add(CreateTopOsdTitleView(sidePadding));
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

            progressRow.Add(_currentTimeLabel);
            progressRow.Add(_progressTrack);
            progressRow.Add(_durationLabel);
            progressRow.Add(_progressThumb);

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
            _audioButton = CreateOsdButton("audio.png", "Audio", 168);
            _subtitleButton = CreateOsdButton("sub.png", "Subtitles", 206);
            _nextButton = CreateOsdButton("next.png", "Next Episode", 242);

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

            CreateAudioOverlay();
            CreateSubtitleOverlay();
            CreateSubtitleText();
            CreateSubtitleViews();
            
            _osdTimer = new Timer(5000);
            _osdTimer.Tick += OnOsdTimerTick;
            _progressTimer = new Timer(500);
            _progressTimer.Tick += (_, __) => { UpdateProgress(); return true; };
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
                ? _sharedResPath + "forward.png"
                : _sharedResPath + "reverse.png";
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
                ResourceUrl = _sharedResPath + "play.png",
                FittingMode = FittingModeType.ShrinkToFit,
                SamplingMode = SamplingModeType.BoxThenLanczos
            };

            _pauseFeedbackIcon = new ImageView
            {
                WidthSpecification = 90,
                HeightSpecification = 90,
                PositionX = 25,
                PositionY = 25,
                ResourceUrl = _sharedResPath + "pause.png",
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
            _durationLabel.Text = FormatTime(duration);
        }

        private string FormatTime(int ms)
        {
            var t = TimeSpan.FromMilliseconds(ms);
            return t.Hours > 0 ? $"{t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}" : $"{t.Minutes:D2}:{t.Seconds:D2}";
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
            _subtitleText = new TextLabel("") { WidthResizePolicy = ResizePolicyType.FillToParent, HeightSpecification = 200, PointSize = 46, TextColor = Color.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, BackgroundColor = Color.Transparent, PositionY = Window.Default.Size.Height - 250, MultiLine = true, LineWrapMode = LineWrapMode.Word, Padding = new Extents(150, 150, 0, 0), EnableMarkup = false };
            _subtitleText.Hide();
            Add(_subtitleText);
        }

        private void CreateSubtitleViews()
        {
            _subtitleImage = new ImageView
            {
                WidthSpecification = 1,
                HeightSpecification = 1,
                PositionX = 0,
                PositionY = 0,
                BackgroundColor = Color.Transparent,
                PreMultipliedAlpha = false,
                FittingMode = FittingModeType.Fill,
                SamplingMode = SamplingModeType.BoxThenLanczos
            };
            _subtitleImage.Hide();
            Add(_subtitleImage);
            _subtitleImage.LowerToBottom();
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
                _useParsedSubtitleRenderer = false;
                _isPgs = false;
                _subtitleCues.Clear();
                _pgsCues.Clear();
                ClearPgsCueCache();
                _pgsRenderToken = 0;
                _pgsHideGraceUntilMs = 0;
                _activeSubtitleCueIndex = -1;
                StopSubtitleRenderTimer();
                try { _player?.ClearSubtitle(); } catch { }
                RunOnUiThread(() =>
                {
                    _subtitleHideTimer?.Stop();
                    _subtitleText?.Hide();
                    _subtitleImage?.Hide();
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
                        _isPgs = false;
                        _subtitleCues.Clear();
                        _pgsCues.Clear();
                        ClearPgsCueCache();
                        _pgsRenderToken = 0;
                        _pgsHideGraceUntilMs = 0;
                        _activeSubtitleCueIndex = -1;
                        StopSubtitleRenderTimer();
                        _subtitleText?.Hide();
                        _subtitleImage?.Hide();
                        _player.SubtitleTrackInfo.Selected = tizenIndex;
                        ApplySubtitleOffset();
                        return;
                    }
                }
                catch {}
            }}

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
            if (_useParsedSubtitleRenderer) return;

            RunOnUiThread(() =>
            {
                string text = e.Text;
                _subtitleHideTimer?.Stop();
                _subtitleImage?.Hide();

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
            StopPlayback();
            _overrideAudioIndex = jellyfinStreamIndex;
            _startPositionMs = (int)currentPos;
            StartPlayback();
        }

        private void ShowOSD()
        {
            var wasVisible = _osdVisible;
            if (!wasVisible) _osdFocusRow = 0;

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
                        UiAnimator.AnimateTo(_subtitleText, "PositionY", (float)(Window.Default.Size.Height - 340), UiAnimator.ScrollDurationMs)
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
                    UiAnimator.AnimateTo(_subtitleText, "PositionY", (float)(Window.Default.Size.Height - 250), UiAnimator.ScrollDurationMs)
                );
            }

            _osdTimer.Stop();
            _progressTimer.Stop();
            HideTrickplayPreview();
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
        {
            if (_suppressPlaybackCompletedNavigation)
                return;

            _suppressPlaybackCompletedNavigation = true;
            _isFinished = true;
            _ = AppState.Jellyfin.MarkAsPlayedAsync(_movie.Id);
            RunOnUiThread(() => { if (_movie.ItemType == "Episode") PlayNextEpisode(); else NavigationService.NavigateBack(); });
        }

        private void StopPlayback()
        {
            _suppressPlaybackCompletedNavigation = true;
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
            _playPauseFeedbackContainer?.Hide();
            ResetTrickplayState();
            _trickplayHttpClient?.Dispose();
            _trickplayHttpClient = null;

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
                try { _player.PlaybackCompleted -= OnPlaybackCompleted; } catch { }
                try { _player.ErrorOccurred -= OnPlayerErrorOccurred; } catch { }
                try { _player.BufferingProgressChanged -= OnBufferingProgressChanged; } catch { }
                _player.SubtitleUpdated -= OnSubtitleUpdated;
                _subtitleHideTimer?.Stop();
                _subtitleText?.Hide();
                _subtitleImage?.Hide();
                _useParsedSubtitleRenderer = false;
                _isPgs = false;
                _subtitleCues.Clear();
                _pgsCues.Clear();
                ClearPgsCueCache();
                _pgsRenderToken = 0;
                _pgsHideGraceUntilMs = 0;
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
        
        private async void PlayNextEpisode() { await SwitchEpisode(1); }

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
            catch (Exception) {NavigationService.NavigateBack(); }
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
            _isPgs = false;
            _subtitleCues.Clear();
            _pgsCues.Clear();
            ClearPgsCueCache();
            _pgsRenderToken = 0;
            _pgsHideGraceUntilMs = 0;
            _activeSubtitleCueIndex = -1;
            StopSubtitleRenderTimer();
            ResetTrickplayState();
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
