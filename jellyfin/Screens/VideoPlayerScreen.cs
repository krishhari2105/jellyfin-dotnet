using System;
using System.Collections.Generic;
using Tizen.Multimedia;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using Tizen.NUI.Components;
using Tizen.Applications;
using JellyfinTizen.Core;
using JellyfinTizen.Models;
using JellyfinTizen.UI;
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
    public partial class VideoPlayerScreen : ScreenBase, IKeyHandler
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
        private bool _activeSubtitleWasExternal;
        private TextLabel _subtitleText;
        private Timer _subtitleHideTimer;
        private DateTime _subtitleHideDeadlineUtc = DateTime.MinValue;
        private int _subtitleTextBaseY;
        private int _subtitleTextOsdY;
        private Timer _subtitleRenderTimer;
        private List<SubtitleCue> _subtitleCues = new List<SubtitleCue>();
        private int[] _subtitleCuePrefixMaxEnd = Array.Empty<int>();
        private bool _useParsedSubtitleRenderer;
        private int _activeSubtitleCueIndex = -1;
        private string _activeParsedSubtitleText = string.Empty;
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
        private string _reportedPlayMethod = "DirectPlay";
        private string _playSessionId;
        private int? _initialSubtitleIndex;
        private int? _requestedEmbeddedSubtitleOrdinal;
        private int? _requestedSubtitleOrdinalAll;
        private string _requestedSubtitleLanguage;
        private string _requestedSubtitleDisplayTitle;
        private string _requestedSubtitleCodec;
        private string _initialSubtitleCodecHint;
        private bool? _requestedSubtitleWasExternal;
        private bool _forceNativeEmbeddedSelectionOnRestart;
        private bool _playerSidecarSubtitleActive;
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
        private View _aspectButton;
        private View _subtitleOffsetTrackContainer;
        private View _subtitleOffsetThumb;
        private View _subtitleOffsetCenterMarker;
        private bool _subtitleOffsetAdjustMode;
        private bool _isAnamorphicVideo;
        private bool _isAspectToggleVisible;
        private bool _useFullscreenAspectMode;
        private int _subtitleOffsetMs;
        private bool _subtitleOffsetBurnInWarningShown;
        private int _osdButtonCount = 1;
        private int _osdFocusRow = 0; // 0 = Seekbar, 1 = Buttons
        private int _buttonFocusIndex = 0;
        private bool _seekbarFocusVisualActive;
        private Animation _osdAnimation;
        private Animation _topOsdAnimation;
        private Animation _subtitleOverlayAnimation;
        private Animation _audioOverlayAnimation;
        private Animation _subtitleTextAnimation;
        private Animation _seekFeedbackAnimation;
        private readonly Dictionary<View, Animation> _focusAnimations = new();
        private readonly Dictionary<string, string> _darkOsdIconPathCache = new(StringComparer.OrdinalIgnoreCase);
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
        private const int ParsedSubtitleRenderTickMs = 100;
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
        private bool _audioSelectionUserOverride;
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
            string preferredMediaSourceId = null,
            int? audioStreamIndex = null,
            string initialSubtitleCodec = null
        )
        {
            _movie = movie;
            _startPositionMs = startPositionMs;
            _initialSubtitleIndex = subtitleStreamIndex;
            _initialSubtitleCodecHint = initialSubtitleCodec;
            _burnIn = burnIn;
            _preferredMediaSourceId = preferredMediaSourceId;
            _overrideAudioIndex = audioStreamIndex;
            _audioSelectionUserOverride = audioStreamIndex.HasValue;
            _trickplayCacheDir = System.IO.Path.Combine(Application.Current.DirectoryInfo.Data, "trickplay-cache");
        }

        public override void OnShow()
        {
            // Delay transparency until the screen is actually shown so
            // details -> player transition can fade cleanly to black.
            Window.Default.BackgroundColor = Color.Transparent;
            BackgroundColor = Color.Transparent;

            // Hidden transport feedback must exist even before OSD is first created.
            EnsureTransientFeedbackViewsCreated();

            // Build OSD lazily on first interaction to avoid heavy view work
            // on the details -> player startup path.
            _useFullscreenAspectMode = false;
            SetAspectToggleVisibility(visible: false);

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
            _smartActionTimer?.Stop();
            CreateSubtitleText();
            CreateStreamDebugOverlay();

            StartPlayback();
        }

        private void EnsureTransientFeedbackViewsCreated()
        {
            if (_seekFeedbackContainer == null)
                CreateSeekFeedback();

            if (_playPauseFeedbackContainer == null)
                CreatePlayPauseFeedback();
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

        private void StartPlayback()
        {
            FireAndForget(StartPlaybackAsync());
        }

        private async Task StartPlaybackAsync()
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
                bool hasSelectedSubtitle = _initialSubtitleIndex.HasValue && _initialSubtitleIndex.Value >= 0;
                bool preferNativeEmbeddedOnStart = _forceNativeEmbeddedSelectionOnRestart && hasSelectedSubtitle && !_burnIn;
                _forceNativeEmbeddedSelectionOnRestart = false;
                int? requestedSubtitleStreamIndex = hasSelectedSubtitle ? _initialSubtitleIndex : -1;
                bool preferTsOnlyHlsForRequestedSubtitle = RequiresTsOnlyHlsProfile(GetRequestedSubtitleCodecHint());
                if (preferNativeEmbeddedOnStart)
                    requestedSubtitleStreamIndex = null;
                string requestedMediaSourceId = _preferredMediaSourceId;
                if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureStreamDebugEvent("StartPlayback.Request", $"sub={requestedSubtitleStreamIndex?.ToString(CultureInfo.InvariantCulture) ?? "OFF"},audio={_overrideAudioIndex?.ToString(CultureInfo.InvariantCulture) ?? "-"},preferredSource={requestedMediaSourceId ?? "-"}");

                _subtitleEnabled = hasSelectedSubtitle;
                if (!hasSelectedSubtitle)
                {
                    _requestedEmbeddedSubtitleOrdinal = null;
                    _requestedSubtitleOrdinalAll = null;
                    _requestedSubtitleLanguage = null;
                    _requestedSubtitleDisplayTitle = null;
                    _requestedSubtitleCodec = null;
                    _initialSubtitleCodecHint = null;
                    _requestedSubtitleWasExternal = null;
                }
                _subtitleOffsetBurnInWarningShown = false;
                _useParsedSubtitleRenderer = false;
                ClearParsedSubtitleCues();
                _activeSubtitleWasExternal = false;
                _playerSidecarSubtitleActive = false;
                if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureSubtitleDebugEvent(
                    "StartPlayback",
                    details: $"token={playbackToken},burnIn={_burnIn},requested={requestedSubtitleStreamIndex?.ToString(CultureInfo.InvariantCulture) ?? "OFF"},nativeRestart={preferNativeEmbeddedOnStart}");
                _player = new Player();

                _player.ErrorOccurred += OnPlayerErrorOccurred;
                _player.BufferingProgressChanged += OnBufferingProgressChanged;
                _player.PlaybackCompleted += OnPlaybackCompleted;
                _player.SubtitleUpdated += OnSubtitleUpdated;

                _player.Display = new Display(Window.Default);
                _player.DisplaySettings.Mode = PlayerDisplayMode.LetterBox;
                _player.DisplaySettings.IsVisible = true;
                _isAnamorphicVideo = false;
                SetAspectToggleVisibility(visible: false);
                bool burnInActive = _burnIn && hasSelectedSubtitle;
                int? pendingStartupNativeEmbeddedSubtitleIndex = null;
                MediaStream pendingStartupNativeEmbeddedSubtitleStream = null;
                MediaStream pendingStartupParsedSubtitleStream = null;

                var playbackInfo = await AppState.Jellyfin.GetPlaybackInfoAsync(
                    playbackMovie.Id,
                    requestedSubtitleStreamIndex,
                    burnInActive,
                    _overrideAudioIndex,
                    subtitleHandlingDisabled: !hasSelectedSubtitle,
                    mediaSourceId: requestedMediaSourceId,
                    preferTsOnlyHls: preferTsOnlyHlsForRequestedSubtitle);
                if (playbackToken != _playbackToken)
                    return;

                // Episode switches can carry a stale media source id from the previous item.
                // Retry once without pinning the source to avoid black-screen stalls.
                if ((playbackInfo?.MediaSources == null || playbackInfo.MediaSources.Count == 0) &&
                    !string.IsNullOrWhiteSpace(requestedMediaSourceId))
                {
                    requestedMediaSourceId = null;
                    var fallbackPlaybackInfo = await AppState.Jellyfin.GetPlaybackInfoAsync(
                        playbackMovie.Id,
                        requestedSubtitleStreamIndex,
                        burnInActive,
                        _overrideAudioIndex,
                        subtitleHandlingDisabled: !hasSelectedSubtitle,
                        mediaSourceId: null,
                        preferTsOnlyHls: preferTsOnlyHlsForRequestedSubtitle);
                    if (playbackToken != _playbackToken)
                        return;

                    if (fallbackPlaybackInfo?.MediaSources != null && fallbackPlaybackInfo.MediaSources.Count > 0)
                        playbackInfo = fallbackPlaybackInfo;
                }

                bool assignedStartupAudioOverride = false;
                if (!_overrideAudioIndex.HasValue)
                {
                    int? startupAudioIndex = ResolveStartupDefaultAudioIndex(ResolvePreferredMediaSource(playbackInfo));
                    if (startupAudioIndex.HasValue)
                    {
                        _overrideAudioIndex = startupAudioIndex.Value;
                        _audioSelectionUserOverride = false;
                        assignedStartupAudioOverride = true;
                        if (DebugSwitches.EnablePlaybackDebugOverlay)
                            CaptureStreamDebugEvent("StartPlayback.AudioDefault", $"index={_overrideAudioIndex.Value}");
                    }
                }

                // Jellyfin applies explicit subtitle/audio stream indices only when MediaSourceId is provided.
                // If details screen did not preselect a source in time, re-request using resolved source id.
                bool shouldRefinePlaybackInfo =
                    (string.IsNullOrWhiteSpace(requestedMediaSourceId) &&
                     (requestedSubtitleStreamIndex.HasValue || _overrideAudioIndex.HasValue)) ||
                    assignedStartupAudioOverride;
                if (shouldRefinePlaybackInfo)
                {
                    string resolvedSourceId = string.IsNullOrWhiteSpace(requestedMediaSourceId)
                        ? ResolvePreferredMediaSource(playbackInfo)?.Id
                        : requestedMediaSourceId;
                    if (!string.IsNullOrWhiteSpace(resolvedSourceId))
                    {
                        requestedMediaSourceId = resolvedSourceId;

                        var refinedPlaybackInfo = await AppState.Jellyfin.GetPlaybackInfoAsync(
                            playbackMovie.Id,
                            requestedSubtitleStreamIndex,
                            burnInActive,
                            _overrideAudioIndex,
                            subtitleHandlingDisabled: !hasSelectedSubtitle,
                            mediaSourceId: requestedMediaSourceId,
                            preferTsOnlyHls: preferTsOnlyHlsForRequestedSubtitle);

                        if (playbackToken != _playbackToken)
                            return;

                        if (refinedPlaybackInfo?.MediaSources != null && refinedPlaybackInfo.MediaSources.Count > 0)
                            playbackInfo = refinedPlaybackInfo;
                    }
                }

                var mediaSource = ResolvePreferredMediaSource(playbackInfo);
                if (mediaSource == null)
                    return;
                _currentMediaSource = mediaSource; 
                AlignRequestedSubtitleIndexForCurrentMediaSource(mediaSource);
                bool startupSubtitleSelectionUnavailable =
                    hasSelectedSubtitle &&
                    HasUnavailableRequestedSubtitleForCurrentMediaSource(mediaSource);
                if (startupSubtitleSelectionUnavailable)
                {
                    ClearRequestedSubtitleSelectionState();
                    burnInActive = false;

                    if (DebugSwitches.EnablePlaybackDebugOverlay)
                        CaptureSubtitleDebugEvent("StartPlayback.ClearUnavailableSubtitle", details: $"mediaSource={mediaSource.Id}");

                    var subtitleOffPlaybackInfo = await AppState.Jellyfin.GetPlaybackInfoAsync(
                        playbackMovie.Id,
                        -1,
                        forceBurnIn: false,
                        audioStreamIndex: _overrideAudioIndex,
                        subtitleHandlingDisabled: true,
                        mediaSourceId: mediaSource.Id);
                    if (playbackToken != _playbackToken)
                        return;

                    var subtitleOffSource = subtitleOffPlaybackInfo?.MediaSources?
                        .FirstOrDefault(s => string.Equals(s.Id, mediaSource.Id, StringComparison.OrdinalIgnoreCase));
                    if (subtitleOffSource == null)
                        subtitleOffSource = ResolvePreferredMediaSource(subtitleOffPlaybackInfo);

                    if (subtitleOffPlaybackInfo?.MediaSources != null &&
                        subtitleOffPlaybackInfo.MediaSources.Count > 0 &&
                        subtitleOffSource != null)
                    {
                        playbackInfo = subtitleOffPlaybackInfo;
                        mediaSource = subtitleOffSource;
                        _currentMediaSource = mediaSource;
                    }
                }

                bool effectiveHasSelectedSubtitle = _initialSubtitleIndex.HasValue && _initialSubtitleIndex.Value >= 0;
                if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureSubtitleTopology("StartPlayback.Subtitles", mediaSource);
                _playSessionId = playbackInfo.PlaySessionId;
                _ = LoadMediaSegmentsAsync();
                UpdateSmartActionTimerState();
                ApplyDisplayModeForCurrentVideo();
                _ = LoadTrickplayInfoAsync();

                string streamUrl = "";
                var apiKey = AppState.AccessToken;
                var serverUrl = AppState.Jellyfin.ServerUrl;
                bool supportsDirectPlay = mediaSource.SupportsDirectPlay;
                bool supportsTranscoding = mediaSource.SupportsTranscoding;
                bool hasTranscodeUrl = !string.IsNullOrEmpty(mediaSource.TranscodingUrl);
                bool requiresServerManagedStream = burnInActive;

                if (supportsDirectPlay && !requiresServerManagedStream)
                {
                    _playMethod = "DirectPlay";
                    streamUrl = $"{serverUrl}/Videos/{playbackMovie.Id}/stream?static=true&MediaSourceId={mediaSource.Id}&PlaySessionId={_playSessionId}&api_key={apiKey}";
                    if (_overrideAudioIndex.HasValue)
                        streamUrl = UpsertQueryParam(streamUrl, "AudioStreamIndex", _overrideAudioIndex.Value.ToString());
                }
                else if (supportsTranscoding || requiresServerManagedStream)
                {
                    _playMethod = "Transcode";
                    if (!hasTranscodeUrl)
                    {
                        if (DebugSwitches.EnablePlaybackDebugOverlay)
                            CaptureStreamDebugEvent("StartPlayback.MissingTranscodingUrl", $"mediaSource={mediaSource.Id},burnIn={burnInActive},supportsTranscoding={supportsTranscoding}");
                        return;
                    }

                    streamUrl = $"{serverUrl}{mediaSource.TranscodingUrl}";

                    string AppendParam(string url, string param) 
                    {
                        if (url.Contains("?")) { if (url.EndsWith("?") || url.EndsWith("&")) return $"{url}{param}"; return $"{url}&{param}"; }
                        return $"{url}?{param}";
                    }

                    if (!streamUrl.Contains("api_key=") && !streamUrl.Contains("Token=")) streamUrl = AppendParam(streamUrl, $"api_key={apiKey}");
                    if (!streamUrl.Contains("PlaySessionId=") && !string.IsNullOrEmpty(_playSessionId)) streamUrl = AppendParam(streamUrl, $"PlaySessionId={_playSessionId}");
                    if (_overrideAudioIndex.HasValue)
                        streamUrl = UpsertQueryParam(streamUrl, "AudioStreamIndex", _overrideAudioIndex.Value.ToString());

                    if (effectiveHasSelectedSubtitle)
                    {
                        bool shouldSendSubtitleIndexToServer =
                            !preferNativeEmbeddedOnStart ||
                            !string.Equals(_playMethod, "DirectPlay", StringComparison.OrdinalIgnoreCase);
                        if (shouldSendSubtitleIndexToServer)
                            streamUrl = UpsertQueryParam(streamUrl, "SubtitleStreamIndex", _initialSubtitleIndex.Value.ToString());
                        if (burnInActive) streamUrl = UpsertQueryParam(streamUrl, "SubtitleMethod", "Encode");
                    }

                    streamUrl = streamUrl.Replace("?&", "?").Replace("&&", "&").Replace(" ", "%20").Replace("\n", "").Replace("\r", "");
                }
                else
                    return;

                string reportedPlayMethod = ResolveReportedPlayMethod(_playMethod, mediaSource, requiresServerManagedStream);
                _reportedPlayMethod = reportedPlayMethod;

                if (!burnInActive)
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

                    if (effectiveHasSelectedSubtitle)
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

                if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureStreamDebugEntry(streamUrl, mediaSource, requiresServerManagedStream, supportsDirectPlay, supportsTranscoding, hasTranscodeUrl);
                if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureSubtitleDebugEvent("StartPlayback.StreamReady", details: $"route={_playMethod},reported={reportedPlayMethod},burnInActive={burnInActive}");

                var source = new MediaUriSource(streamUrl);
                SetReportingContext(playbackMovie.Id, _playSessionId, mediaSource.Id, reportedPlayMethod);
                _player.SetSource(source);

                await _player.PrepareAsync();
                if (playbackToken != _playbackToken)
                    return;

                ApplyDisplayModeForCurrentVideo();
                ApplyPendingNativeAudioOverride(playbackToken, mediaSource);

                try { _ = _player.StreamInfo.GetVideoProperties(); } catch { }
                try { _ = _player.AudioTrackInfo.GetCount(); } catch { }
                try 
                {
                    if (!burnInActive && effectiveHasSelectedSubtitle)
                    {
                        var selectedSubStream = mediaSource.MediaStreams?
                            .FirstOrDefault(s => s.Type == "Subtitle" && s.Index == _initialSubtitleIndex.Value);
                        if (selectedSubStream != null)
                        {
                            _initialSubtitleCodecHint = selectedSubStream.Codec;
                            _requestedSubtitleCodec = selectedSubStream.Codec;
                            _requestedSubtitleLanguage = selectedSubStream.Language;
                            _requestedSubtitleDisplayTitle = selectedSubStream.DisplayTitle;
                            _requestedSubtitleWasExternal = selectedSubStream.IsExternal;
                            _requestedEmbeddedSubtitleOrdinal = selectedSubStream.IsExternal
                                ? null
                                : GetEmbeddedSubtitleOrdinal(mediaSource, selectedSubStream.Index);
                            var orderedSubtitleStreams = mediaSource.MediaStreams?
                                .Where(s => string.Equals(s.Type, "Subtitle", StringComparison.OrdinalIgnoreCase))
                                .OrderBy(s => s.Index)
                                .ToList();
                            _requestedSubtitleOrdinalAll = orderedSubtitleStreams?.FindIndex(s => s.Index == selectedSubStream.Index);
                            if (_requestedSubtitleOrdinalAll.HasValue && _requestedSubtitleOrdinalAll.Value < 0)
                                _requestedSubtitleOrdinalAll = null;

                            _activeSubtitleWasExternal = selectedSubStream.IsExternal;
                            if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureSubtitleDebugEvent("StartPlayback.ApplySubtitle", selectedSubStream, "phase=prepared");
                            bool isDirectPlay = string.Equals(_playMethod, "DirectPlay", StringComparison.OrdinalIgnoreCase);
                            bool useNativeEmbedded =
                                isDirectPlay &&
                                !selectedSubStream.IsExternal &&
                                !RequiresServerTranscodeSubtitleSwitch(selectedSubStream);
                            if (useNativeEmbedded)
                            {
                                pendingStartupNativeEmbeddedSubtitleIndex = _initialSubtitleIndex.Value;
                                pendingStartupNativeEmbeddedSubtitleStream = selectedSubStream;
                                if (DebugSwitches.EnablePlaybackDebugOverlay)
                                    CaptureSubtitleDebugEvent("StartPlayback.NativeDeferred", selectedSubStream, "phase=prepared");
                            }
                            else
                            {
                                bool downloadedApplied = await DownloadAndSetSubtitle(mediaSource.Id, selectedSubStream);
                                if (downloadedApplied && _useParsedSubtitleRenderer)
                                    pendingStartupParsedSubtitleStream = selectedSubStream;
                                if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureSubtitleDebugEvent(
                                    "StartPlayback.DownloadedApply",
                                    selectedSubStream,
                                    $"result={downloadedApplied},isExternal={selectedSubStream.IsExternal},playMethod={_playMethod}");
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
                ApplyPendingNativeAudioOverride(playbackToken, mediaSource);
                if (_useParsedSubtitleRenderer && pendingStartupParsedSubtitleStream != null)
                {
                    bool parserNativeOffStable = await StabilizeStartupParsedSubtitleRendererAsync(
                        pendingStartupParsedSubtitleStream,
                        playbackToken);
                    if (DebugSwitches.EnablePlaybackDebugOverlay)
                        CaptureSubtitleDebugEvent(
                            "StartPlayback.ParserPostStart",
                            pendingStartupParsedSubtitleStream,
                            $"result={parserNativeOffStable},playMethod={_playMethod}");
                }
                if (pendingStartupNativeEmbeddedSubtitleIndex.HasValue)
                {
                    var switchableEmbedded = GetNativeSwitchableEmbeddedSubtitleStreams(mediaSource);
                    int targetSwitchableOrdinal = switchableEmbedded.FindIndex(s => s.Index == pendingStartupNativeEmbeddedSubtitleIndex.Value);
                    bool hasUnsupportedEmbedded = HasUnsupportedEmbeddedSubtitleStreams(mediaSource);
                    bool primeEnabled = hasUnsupportedEmbedded && targetSwitchableOrdinal > 0 && switchableEmbedded.Count > 0;
                    var pendingStream = mediaSource.MediaStreams?
                        .FirstOrDefault(s =>
                            string.Equals(s.Type, "Subtitle", StringComparison.OrdinalIgnoreCase) &&
                            s.Index == pendingStartupNativeEmbeddedSubtitleIndex.Value);

                    if (DebugSwitches.EnablePlaybackDebugOverlay)
                    {
                        CaptureStreamDebugEvent(
                            "StartPlayback.NativePrimeGate",
                            $"target={pendingStartupNativeEmbeddedSubtitleIndex.Value},targetOrd={targetSwitchableOrdinal},unsupportedEmbedded={(hasUnsupportedEmbedded ? "yes" : "no")},prime={(primeEnabled ? "yes" : "no")},phase=postStartCallsite");
                        CaptureSubtitleDebugEvent(
                            "StartPlayback.NativePrimeGate",
                            pendingStream,
                            $"target={pendingStartupNativeEmbeddedSubtitleIndex.Value},targetOrd={targetSwitchableOrdinal},unsupportedEmbedded={(hasUnsupportedEmbedded ? "yes" : "no")},prime={(primeEnabled ? "yes" : "no")},phase=postStartCallsite");
                    }

                    bool startupNativeResult = await TryApplyStartupNativeEmbeddedSubtitleAsync(
                        pendingStartupNativeEmbeddedSubtitleIndex.Value,
                        playbackToken);
                    if (DebugSwitches.EnablePlaybackDebugOverlay)
                        CaptureSubtitleDebugEvent(
                            "StartPlayback.NativePostStart",
                            pendingStartupNativeEmbeddedSubtitleStream,
                            $"result={startupNativeResult},attempts=postStart,prime={(primeEnabled ? "yes" : "no")},unsupportedEmbedded={(hasUnsupportedEmbedded ? "yes" : "no")},targetOrd={targetSwitchableOrdinal}");
                }
                ApplySubtitleOffset();
                _ = RefreshAnamorphicStateAsync(playbackMovie.Id, playbackToken);

                var info = new PlaybackProgressInfo
                {
                    ItemId = playbackMovie.Id, PlaySessionId = _playSessionId, MediaSourceId = mediaSource.Id,
                    PositionTicks = _startPositionMs * 10000, IsPaused = false, PlayMethod = reportedPlayMethod, EventName = "TimeUpdate"
                };
                _ = AppState.Jellyfin.ReportPlaybackStartAsync(info);
            }
            catch (Exception ex)
            {
                if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureStreamDebugEvent("StartPlayback.Exception", ex.Message);
                if (playbackToken == _playbackToken)
                    ClearReportingContext();
            }
        }

        private async Task<bool> DownloadAndSetSubtitle(string mediaSourceId, MediaStream subtitleStream)
        {
            try
            {
                bool isDirectPlay = string.Equals(_playMethod, "DirectPlay", StringComparison.OrdinalIgnoreCase);
                bool canDownloadSubtitle = subtitleStream != null && (subtitleStream.IsExternal || !isDirectPlay);
                if (!canDownloadSubtitle)
                {
                    string reason = subtitleStream == null ? "nullStream" : $"unsupportedDirectPlayEmbedded(method={_playMethod})";
                    if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureSubtitleDebugEvent("Subtitle.DownloadSkip", subtitleStream, $"reason={reason}");
                    return false;
                }

                subtitleStream = await ResolveSubtitleDeliveryStreamAsync(mediaSourceId, subtitleStream);

                if (DebugSwitches.EnablePlaybackDebugOverlay)
                {
                    string deliveryRaw = SanitizeDebugStreamUrl(subtitleStream?.DeliveryUrl);
                    string deliveryRawExt = TryExtractSubtitleUrlExtension(subtitleStream?.DeliveryUrl);
                    string codec = string.IsNullOrWhiteSpace(subtitleStream?.Codec) ? "-" : subtitleStream.Codec.ToLowerInvariant();
                    CaptureSubtitleDebugEvent(
                        "Subtitle.DownloadStart",
                        subtitleStream,
                        $"mediaSource={mediaSourceId},playMethod={_playMethod},codec={codec},deliveryExt={deliveryRawExt},deliveryUrl={deliveryRaw}");
                }
                ApplySubtitleTextStyle();

                int subtitleIndex = subtitleStream.Index;
                var apiKey = AppState.AccessToken;
                const bool allowMissingDeliveryFallback = false;
                var downloadUrl = BuildSubtitleDeliveryUrl(
                    subtitleStream,
                    mediaSourceId,
                    subtitleIndex,
                    "srt",
                    allowFallbackWhenMissingDelivery: allowMissingDeliveryFallback,
                    forceFallback: false);
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    if (DebugSwitches.EnablePlaybackDebugOverlay)
                        CaptureSubtitleDebugEvent("Subtitle.DownloadSkip", subtitleStream, "reason=missingDeliveryUrlNoFallback");
                    return false;
                }

                string ext = ResolveSubtitleExtension(downloadUrl, subtitleStream.Codec);
                if (DebugSwitches.EnablePlaybackDebugOverlay)
                {
                    string resolvedUrl = SanitizeDebugStreamUrl(downloadUrl);
                    string source = !string.IsNullOrWhiteSpace(subtitleStream?.DeliveryUrl) ? "deliveryUrl" : "fallbackPath";
                    CaptureSubtitleDebugEvent(
                        "Subtitle.DownloadResolvedUrl",
                        subtitleStream,
                        $"source={source},resolvedExt={ext},resolvedUrl={resolvedUrl}");
                }
                var localPath = System.IO.Path.Combine(Application.Current.DirectoryInfo.Data, $"sub_{mediaSourceId}_{subtitleIndex}.{ext}");
                string authHeader = AppState.Jellyfin?.BuildAuthorizationHeader(apiKey);

                using (var client = new HttpClient())
                {
                    if (!string.IsNullOrWhiteSpace(authHeader))
                    {
                        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authHeader);
                        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Emby-Authorization", authHeader);
                    }
                    if (!string.IsNullOrWhiteSpace(apiKey))
                        client.DefaultRequestHeaders.TryAddWithoutValidation("X-MediaBrowser-Token", apiKey);

                    using var response = await client.GetAsync(downloadUrl);
                    response.EnsureSuccessStatusCode();
                    var data = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(localPath, data);
                }
                _externalSubtitlePath = localPath;

                // Prefer app-rendered external subtitles to avoid sidecar/native switch glitches on Tizen.
                bool allowParsedRenderer = !_burnIn;
                if (!allowParsedRenderer || !TryLoadSubtitleCues(localPath))
                {
                    _useParsedSubtitleRenderer = false;
                    _playerSidecarSubtitleActive = false;
                    _subtitleText?.Hide();
                    StopSubtitleRenderTimer();
                    if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureSubtitleDebugEvent("Subtitle.DownloadSkip", subtitleStream, "reason=parserRequired");
                    return false;
                }

                _useParsedSubtitleRenderer = true;
                _playerSidecarSubtitleActive = false;
                TryDisableNativeSubtitleTrack();
                StartSubtitleRenderTimer();

                _activeSubtitleWasExternal = true;
                if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureSubtitleDebugEvent("Subtitle.DownloadApplied", subtitleStream, $"parsed={_useParsedSubtitleRenderer},ext={ext},path={System.IO.Path.GetFileName(localPath)}");
                return true;
            }
            catch (Exception)
            {
                _externalSubtitlePath = null;
                _useParsedSubtitleRenderer = false;
                _playerSidecarSubtitleActive = false;
                ClearParsedSubtitleCues();
                StopSubtitleRenderTimer();
                if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureSubtitleDebugEvent("Subtitle.DownloadFailed", subtitleStream);
                return false;
            }
        }

        private async Task<MediaStream> ResolveSubtitleDeliveryStreamAsync(string mediaSourceId, MediaStream subtitleStream)
        {
            if (subtitleStream == null)
                return null;

            if (!string.IsNullOrWhiteSpace(subtitleStream.DeliveryUrl))
                return subtitleStream;

            if (string.IsNullOrWhiteSpace(_movie?.Id) || string.IsNullOrWhiteSpace(mediaSourceId))
                return subtitleStream;

            try
            {
                if (DebugSwitches.EnablePlaybackDebugOverlay)
                    CaptureSubtitleDebugEvent("Subtitle.ResolveDeliveryStart", subtitleStream, $"mediaSource={mediaSourceId},playMethod={_playMethod}");

                var refreshedInfo = await AppState.Jellyfin.GetPlaybackInfoAsync(
                    _movie.Id,
                    subtitleStream.Index,
                    forceBurnIn: false,
                    audioStreamIndex: _overrideAudioIndex,
                    subtitleHandlingDisabled: false,
                    mediaSourceId: mediaSourceId);

                var refreshedSource = refreshedInfo?.MediaSources?
                    .FirstOrDefault(s => string.Equals(s.Id, mediaSourceId, StringComparison.OrdinalIgnoreCase));
                if (refreshedSource == null)
                    refreshedSource = ResolvePreferredMediaSource(refreshedInfo);

                var refreshedStream = refreshedSource?.MediaStreams?
                    .FirstOrDefault(s => string.Equals(s.Type, "Subtitle", StringComparison.OrdinalIgnoreCase) && s.Index == subtitleStream.Index);

                if (refreshedStream == null || string.IsNullOrWhiteSpace(refreshedStream.DeliveryUrl))
                {
                    if (DebugSwitches.EnablePlaybackDebugOverlay)
                        CaptureSubtitleDebugEvent("Subtitle.ResolveDeliveryMiss", subtitleStream, "reason=missingAfterRefresh");
                    return subtitleStream;
                }

                // Keep current playback source context, only refresh subtitle delivery metadata.
                subtitleStream.DeliveryUrl = refreshedStream.DeliveryUrl;
                if (string.IsNullOrWhiteSpace(subtitleStream.Codec))
                    subtitleStream.Codec = refreshedStream.Codec;
                subtitleStream.IsExternal = subtitleStream.IsExternal || refreshedStream.IsExternal;

                if (DebugSwitches.EnablePlaybackDebugOverlay)
                    CaptureSubtitleDebugEvent("Subtitle.ResolveDeliveryApplied", subtitleStream, $"deliveryExt={TryExtractSubtitleUrlExtension(subtitleStream.DeliveryUrl)}");

                return subtitleStream;
            }
            catch
            {
                if (DebugSwitches.EnablePlaybackDebugOverlay)
                    CaptureSubtitleDebugEvent("Subtitle.ResolveDeliveryFail", subtitleStream);
                return subtitleStream;
            }
        }

        private bool TryLoadSubtitleCues(string path)
        {
            string ext = string.Empty;
            try { ext = System.IO.Path.GetExtension(path)?.TrimStart('.').ToLowerInvariant() ?? string.Empty; } catch { }

            if (ext == "ass" || ext == "ssa")
            {
                if (TryLoadAssSubtitleCues(path)) return true;
                if (TryLoadSrtSubtitleCues(path)) return true;
                return TryLoadVttSubtitleCues(path);
            }

            if (ext == "vtt" || ext == "webvtt")
            {
                if (TryLoadVttSubtitleCues(path)) return true;
                if (TryLoadSrtSubtitleCues(path)) return true;
                return TryLoadAssSubtitleCues(path);
            }

            if (TryLoadSrtSubtitleCues(path)) return true;
            if (TryLoadVttSubtitleCues(path)) return true;
            return TryLoadAssSubtitleCues(path);
        }

        private void ClearParsedSubtitleCues()
        {
            _subtitleCues.Clear();
            _subtitleCuePrefixMaxEnd = Array.Empty<int>();
            _activeSubtitleCueIndex = -1;
            _activeParsedSubtitleText = string.Empty;
        }

        private void SetParsedSubtitleCues(List<SubtitleCue> cues)
        {
            if (cues == null || cues.Count == 0)
            {
                ClearParsedSubtitleCues();
                return;
            }

            cues.Sort((a, b) => a.StartMs.CompareTo(b.StartMs));
            _subtitleCues = cues;
            RebuildParsedCuePrefixMaxEnd();
            _activeSubtitleCueIndex = -1;
        }

        private void RebuildParsedCuePrefixMaxEnd()
        {
            int count = _subtitleCues.Count;
            if (count == 0)
            {
                _subtitleCuePrefixMaxEnd = Array.Empty<int>();
                return;
            }

            _subtitleCuePrefixMaxEnd = new int[count];
            int prefixMaxEnd = int.MinValue;
            for (int i = 0; i < count; i++)
            {
                if (_subtitleCues[i].EndMs > prefixMaxEnd)
                    prefixMaxEnd = _subtitleCues[i].EndMs;
                _subtitleCuePrefixMaxEnd[i] = prefixMaxEnd;
            }
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
            else if (ext == "webvtt")
                ext = "vtt";
            else if (ext == "ssa")
                ext = "ass";

            return ext;
        }

        private static string TryExtractSubtitleUrlExtension(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return "-";

            string pathPart = NormalizeSubtitleDeliveryUrl(url);
            int queryIndex = pathPart.IndexOf('?');
            if (queryIndex >= 0) pathPart = pathPart.Substring(0, queryIndex);

            string candidate = System.IO.Path.GetExtension(pathPart);
            if (string.IsNullOrWhiteSpace(candidate))
                return "-";

            return candidate.TrimStart('.').ToLowerInvariant();
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
                            Text = NormalizeParsedCueText(cueText)
                        });
                    }
                }

                SetParsedSubtitleCues(cues);
                return _subtitleCues.Count > 0;
            }
            catch
            {
                ClearParsedSubtitleCues();
                return false;
            }
        }

        private string BuildSubtitleDeliveryUrl(
            MediaStream subtitleStream,
            string mediaSourceId,
            int subtitleIndex,
            string ext,
            bool allowFallbackWhenMissingDelivery = true,
            bool forceFallback = false)
        {
            if (subtitleStream == null) return null;

            var serverUrl = AppState.Jellyfin.ServerUrl?.TrimEnd('/');
            var apiKey = AppState.AccessToken;

            var deliveryUrl = subtitleStream.DeliveryUrl;
            if (!forceFallback && !string.IsNullOrWhiteSpace(deliveryUrl))
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
            if (!allowFallbackWhenMissingDelivery)
                return null;

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

        private static bool IsSubtitleCodecRuntimeSwitchSupported(string codec)
        {
            if (string.IsNullOrWhiteSpace(codec))
                return false;

            string normalizedCodec = codec.Trim().ToLowerInvariant();
            var profile = ProfileBuilder.BuildTizenProfile(forceBurnIn: false, disableSubtitles: false);
            var subtitleProfiles = profile?.SubtitleProfiles;
            if (subtitleProfiles == null || subtitleProfiles.Count == 0)
                return false;

            return subtitleProfiles.Any(p =>
                !string.IsNullOrWhiteSpace(p?.Format) &&
                (string.Equals(p.Method, "External", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(p.Method, "Embed", StringComparison.OrdinalIgnoreCase)) &&
                string.Equals(p.Format.Trim(), normalizedCodec, StringComparison.OrdinalIgnoreCase));
        }

        private static bool RequiresServerTranscodeSubtitleSwitch(MediaStream stream)
        {
            if (stream == null)
                return false;

            // If subtitle codec is not advertised as runtime-switchable by our device profile,
            // we must renegotiate playback so server can render it in the transcoded stream.
            return !IsSubtitleCodecRuntimeSwitchSupported(stream.Codec);
        }

        private static bool RequiresTsOnlyHlsProfile(string codec)
        {
            if (string.IsNullOrWhiteSpace(codec))
                return false;

            string normalizedCodec = codec.Trim().ToLowerInvariant();
            return string.Equals(normalizedCodec, "dvdsub", StringComparison.Ordinal) ||
                   string.Equals(normalizedCodec, "vobsub", StringComparison.Ordinal);
        }

        private static MediaStream GetSelectedSubtitleStream(MediaSourceInfo mediaSource, int? subtitleStreamIndex)
        {
            if (mediaSource?.MediaStreams == null || !subtitleStreamIndex.HasValue || subtitleStreamIndex.Value < 0)
                return null;

            return mediaSource.MediaStreams.FirstOrDefault(s =>
                string.Equals(s.Type, "Subtitle", StringComparison.OrdinalIgnoreCase) &&
                s.Index == subtitleStreamIndex.Value);
        }

        private string GetRequestedSubtitleCodecHint()
        {
            if (!_initialSubtitleIndex.HasValue || _initialSubtitleIndex.Value < 0)
                return null;

            if (!string.IsNullOrWhiteSpace(_requestedSubtitleCodec))
                return _requestedSubtitleCodec;

            if (!string.IsNullOrWhiteSpace(_initialSubtitleCodecHint))
                return _initialSubtitleCodecHint;

            return GetSelectedSubtitleStream(_currentMediaSource, _initialSubtitleIndex)?.Codec;
        }

        private static string NormalizeSubtitleLanguageKey(string rawLanguage)
        {
            if (string.IsNullOrWhiteSpace(rawLanguage))
                return string.Empty;

            string token = rawLanguage.Trim().ToLowerInvariant();
            int sep = token.IndexOfAny(new[] { '-', '_', ' ', '(', ')' });
            if (sep > 0)
                token = token.Substring(0, sep);

            if (token.Length == 2)
                return token;

            if (token.Length == 3)
            {
                foreach (var culture in CultureInfo.GetCultures(CultureTypes.NeutralCultures))
                {
                    if (culture == null)
                        continue;

                    string three = culture.ThreeLetterISOLanguageName?.ToLowerInvariant() ?? string.Empty;
                    if (string.Equals(three, token, StringComparison.Ordinal))
                    {
                        string two = culture.TwoLetterISOLanguageName?.ToLowerInvariant() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(two) && two.Length == 2)
                            return two;
                    }
                }

                return token switch
                {
                    "eng" => "en",
                    "dan" => "da",
                    "fin" => "fi",
                    "fra" => "fr",
                    "fre" => "fr",
                    "deu" => "de",
                    "ger" => "de",
                    "spa" => "es",
                    "por" => "pt",
                    "nor" => "no",
                    "nob" => "no",
                    "nno" => "nn",
                    _ => token
                };
            }

            return token;
        }

        private bool TryGetNativeSubtitleLanguageKey(int tizenIndex, out string languageKey)
        {
            languageKey = string.Empty;
            try
            {
                var trackInfo = _player?.SubtitleTrackInfo;
                if (trackInfo == null)
                    return false;

                var getLanguageMethod = trackInfo.GetType().GetMethod("GetLanguageCode", new[] { typeof(int) });
                if (getLanguageMethod == null)
                    return false;

                string rawLanguage = getLanguageMethod.Invoke(trackInfo, new object[] { tizenIndex }) as string;
                languageKey = NormalizeSubtitleLanguageKey(rawLanguage);
                return !string.IsNullOrWhiteSpace(languageKey);
            }
            catch
            {
                return false;
            }
        }

        private int ResolveNativeSubtitleSlotForStream(MediaStream targetStream, List<MediaStream> switchableEmbedded, int tizenSubtitleCount)
        {
            if (targetStream == null || switchableEmbedded == null || switchableEmbedded.Count == 0 || tizenSubtitleCount <= 0)
                return -1;

            int defaultOrdinal = switchableEmbedded.FindIndex(s => s.Index == targetStream.Index);
            if (defaultOrdinal < 0)
                return -1;

            string targetLanguageKey = NormalizeSubtitleLanguageKey(targetStream.Language);
            if (string.IsNullOrWhiteSpace(targetLanguageKey))
                return defaultOrdinal;

            int desiredLanguageOccurrence = 0;
            for (int i = 0; i < defaultOrdinal; i++)
            {
                var stream = switchableEmbedded[i];
                if (string.Equals(NormalizeSubtitleLanguageKey(stream?.Language), targetLanguageKey, StringComparison.Ordinal))
                    desiredLanguageOccurrence++;
            }

            var nativeLanguageSlots = new List<int>();
            for (int i = 0; i < tizenSubtitleCount; i++)
            {
                if (TryGetNativeSubtitleLanguageKey(i, out var nativeLanguageKey) &&
                    string.Equals(nativeLanguageKey, targetLanguageKey, StringComparison.Ordinal))
                {
                    nativeLanguageSlots.Add(i);
                }
            }

            if (nativeLanguageSlots.Count == 0)
                return defaultOrdinal;

            if (desiredLanguageOccurrence >= 0 && desiredLanguageOccurrence < nativeLanguageSlots.Count)
                return nativeLanguageSlots[desiredLanguageOccurrence];

            return nativeLanguageSlots[nativeLanguageSlots.Count - 1];
        }

        private bool TrySelectNativeEmbeddedSubtitle(int jellyfinStreamIndex)
        {
            if (_player == null || _currentMediaSource == null)
            {
                if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureSubtitleDebugEvent("Subtitle.NativeSelectFail", details: "reason=playerOrMediaSourceMissing");
                return false;
            }

            if (_playMethod != "DirectPlay")
            {
                if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureSubtitleDebugEvent("Subtitle.NativeSelectFail", details: $"reason=playMethod:{_playMethod}");
                return false;
            }

            try
            {
                if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureSubtitleTopology("Subtitle.NativeSelectTopology", _currentMediaSource);
                try { _player.ClearSubtitle(); } catch { }
                _externalSubtitlePath = null;
                _playerSidecarSubtitleActive = false;

                int tizenSubtitleCount = _player.SubtitleTrackInfo.GetCount();
                if (tizenSubtitleCount <= 0)
                {
                    if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureSubtitleDebugEvent("Subtitle.NativeSelectFail", details: "reason=noNativeTracks");
                    return false;
                }

                var embeddedSubs = GetNativeSwitchableEmbeddedSubtitleStreams(_currentMediaSource);
                if (embeddedSubs == null || embeddedSubs.Count == 0)
                {
                    if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureSubtitleDebugEvent("Subtitle.NativeSelectFail", details: "reason=noNativeSwitchableEmbeddedStreamsInMetadata");
                    return false;
                }

                var targetStream = embeddedSubs.FirstOrDefault(s => s.Index == jellyfinStreamIndex);
                int defaultOrdinal = embeddedSubs.FindIndex(s => s.Index == jellyfinStreamIndex);
                int tizenIndex = ResolveNativeSubtitleSlotForStream(targetStream, embeddedSubs, tizenSubtitleCount);
                if (DebugSwitches.EnablePlaybackDebugOverlay)
                {
                    string targetLang = NormalizeSubtitleLanguageKey(targetStream?.Language);
                    CaptureSubtitleDebugEvent("Subtitle.NativeSelectAttempt", targetStream, $"targetJellyfin={jellyfinStreamIndex},targetTizen={tizenIndex},defaultOrd={defaultOrdinal},targetLang={targetLang},nativeCount={tizenSubtitleCount}");
                }
                if (tizenIndex < 0 || tizenIndex >= tizenSubtitleCount)
                {
                    if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureSubtitleDebugEvent("Subtitle.NativeSelectFail", targetStream, $"reason=indexMismatch,targetTizen={tizenIndex},nativeCount={tizenSubtitleCount}");
                    return false;
                }

                _useParsedSubtitleRenderer = false;
                ClearParsedSubtitleCues();
                StopSubtitleRenderTimer();
                _subtitleText?.Hide();
                try { _player.SubtitleTrackInfo.Selected = -1; } catch { }
                _player.SubtitleTrackInfo.Selected = tizenIndex;
                int selectedAfterSet = -2;
                try { selectedAfterSet = _player.SubtitleTrackInfo.Selected; } catch { }
                if (selectedAfterSet != tizenIndex)
                {
                    if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureSubtitleDebugEvent("Subtitle.NativeSelectFail", targetStream, $"reason=selectedMismatch,expected={tizenIndex},actual={selectedAfterSet}");
                    return false;
                }
                _subtitleEnabled = true;
                _activeSubtitleWasExternal = false;
                _initialSubtitleIndex = jellyfinStreamIndex;
                SyncSubtitleSelectionFromCurrentState();
                ApplySubtitleTextStyle();
                if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureSubtitleDebugEvent("Subtitle.NativeSelectSuccess", targetStream, $"selectedTizen={selectedAfterSet}");
                return true;
            }
            catch
            {
                if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureSubtitleDebugEvent("Subtitle.NativeSelectFail", details: "reason=exception");
                return false;
            }
        }

        private async Task<bool> TryApplyStartupNativeEmbeddedSubtitleAsync(int jellyfinStreamIndex, int playbackToken)
        {
            var switchableEmbedded = GetNativeSwitchableEmbeddedSubtitleStreams(_currentMediaSource);
            var targetStream = switchableEmbedded.FirstOrDefault(s => s.Index == jellyfinStreamIndex);
            int targetSwitchableOrdinal = switchableEmbedded.FindIndex(s => s.Index == jellyfinStreamIndex);
            bool hasUnsupportedEmbeddedSubtitles = HasUnsupportedEmbeddedSubtitleStreams(_currentMediaSource);
            int? primeFirstEmbeddedIndex =
                hasUnsupportedEmbeddedSubtitles && targetSwitchableOrdinal > 0 && switchableEmbedded.Count > 0
                    ? switchableEmbedded[0].Index
                    : null;

            if (DebugSwitches.EnablePlaybackDebugOverlay)
            {
                CaptureStreamDebugEvent(
                    "StartPlayback.NativePrimeGate",
                    $"target={jellyfinStreamIndex},targetOrd={targetSwitchableOrdinal},unsupportedEmbedded={(hasUnsupportedEmbeddedSubtitles ? "yes" : "no")},prime={(primeFirstEmbeddedIndex.HasValue ? "yes" : "no")}");
            }

            const int maxAttempts = 9;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (playbackToken != _playbackToken || _player == null)
                    return false;

                if (attempt > 0)
                    await Task.Delay(130 + (attempt * 45));

                if (playbackToken != _playbackToken || _player == null)
                    return false;

                if (primeFirstEmbeddedIndex.HasValue)
                {
                    TrySelectNativeEmbeddedSubtitle(primeFirstEmbeddedIndex.Value);
                    await Task.Delay(220);

                    if (playbackToken != _playbackToken || _player == null)
                        return false;
                }

                if (TrySelectNativeEmbeddedSubtitle(jellyfinStreamIndex))
                {
                    bool needsStabilizeReapply = primeFirstEmbeddedIndex.HasValue;
                    if (!needsStabilizeReapply)
                        return true;

                    bool stabilizeResult = await StabilizeStartupNativeSubtitleSelectionAsync(
                        jellyfinStreamIndex,
                        targetStream,
                        switchableEmbedded,
                        targetSwitchableOrdinal,
                        playbackToken,
                        attempt + 1);
                    if (stabilizeResult)
                        return true;
                }

                if (DebugSwitches.EnablePlaybackDebugOverlay)
                    CaptureStreamDebugEvent("StartPlayback.NativePostStartRetry", $"attempt={attempt + 1},target={jellyfinStreamIndex},prime={(primeFirstEmbeddedIndex.HasValue ? "yes" : "no")}");
            }

            return false;
        }

        private async Task<bool> StabilizeStartupNativeSubtitleSelectionAsync(
            int jellyfinStreamIndex,
            MediaStream targetStream,
            List<MediaStream> switchableEmbedded,
            int targetSwitchableOrdinal,
            int playbackToken,
            int attemptNumber)
        {
            const int stabilizeWindowMs = 600;
            const int stabilizePollMs = 90;
            const int stableChecksRequired = 2;
            int maxTicks = (int)Math.Ceiling((double)stabilizeWindowMs / stabilizePollMs);
            bool observedMismatch = false;
            int stableChecks = 0;

            if (DebugSwitches.EnablePlaybackDebugOverlay)
            {
                CaptureStreamDebugEvent(
                    "StartPlayback.NativeStabilizeWatchdog",
                    $"target={jellyfinStreamIndex},targetOrd={targetSwitchableOrdinal},attempt={attemptNumber},windowMs={stabilizeWindowMs},pollMs={stabilizePollMs}");
            }

            for (int tick = 1; tick <= maxTicks; tick++)
            {
                await Task.Delay(stabilizePollMs);
                if (playbackToken != _playbackToken || _player == null)
                    return true;

                int nativeCount = GetNativeSubtitleTrackCountSafe();
                int expected = ResolveNativeSubtitleSlotForStream(targetStream, switchableEmbedded, nativeCount);
                int actual = GetSelectedNativeSubtitleTrackSafe();
                bool comparable = expected >= 0 && actual >= 0;
                bool mismatch = comparable && actual != expected;
                bool stable = comparable && actual == expected;

                if (mismatch)
                {
                    observedMismatch = true;
                    stableChecks = 0;

                    bool reapplyResult = TrySelectNativeEmbeddedSubtitle(jellyfinStreamIndex);
                    if (DebugSwitches.EnablePlaybackDebugOverlay)
                    {
                        CaptureStreamDebugEvent(
                            "StartPlayback.NativeStabilizeReapply",
                            $"target={jellyfinStreamIndex},targetOrd={targetSwitchableOrdinal},attempt={attemptNumber},tick={tick},expected={expected},actual={actual},result={reapplyResult}");
                        CaptureSubtitleDebugEvent(
                            "Subtitle.NativePostSelectMismatch",
                            targetStream,
                            $"target={jellyfinStreamIndex},attempt={attemptNumber},tick={tick},expected={expected},actual={actual},reapplyResult={reapplyResult}");
                    }

                    if (!reapplyResult)
                        return false;

                    continue;
                }

                if (stable)
                {
                    stableChecks++;
                    if (stableChecks >= stableChecksRequired)
                    {
                        if (DebugSwitches.EnablePlaybackDebugOverlay)
                        {
                            CaptureStreamDebugEvent(
                                "StartPlayback.NativeStabilizeComplete",
                                $"target={jellyfinStreamIndex},targetOrd={targetSwitchableOrdinal},attempt={attemptNumber},tick={tick},result=stable,mismatch={(observedMismatch ? "yes" : "no")},expected={expected},actual={actual}");
                        }

                        return true;
                    }
                }
                else
                {
                    stableChecks = 0;
                }
            }

            bool successAfterWindow = !observedMismatch || stableChecks > 0;
            if (DebugSwitches.EnablePlaybackDebugOverlay)
            {
                CaptureStreamDebugEvent(
                    "StartPlayback.NativeStabilizeComplete",
                    $"target={jellyfinStreamIndex},targetOrd={targetSwitchableOrdinal},attempt={attemptNumber},result={(successAfterWindow ? "ok" : "retry")},mismatch={(observedMismatch ? "yes" : "no")},stableChecks={stableChecks}");
                CaptureSubtitleDebugEvent(
                    "Subtitle.NativePostSelectCheck",
                    targetStream,
                    $"target={jellyfinStreamIndex},attempt={attemptNumber},windowMs={stabilizeWindowMs},mismatch={(observedMismatch ? "yes" : "no")},stableChecks={stableChecks},result={(successAfterWindow ? "ok" : "retry")}");
            }

            return successAfterWindow;
        }

        private async Task<bool> StabilizeStartupParsedSubtitleRendererAsync(MediaStream targetStream, int playbackToken)
        {
            const int stabilizeWindowMs = 600;
            const int stabilizePollMs = 90;
            const int stableChecksRequired = 2;
            int maxTicks = (int)Math.Ceiling((double)stabilizeWindowMs / stabilizePollMs);
            bool observedNativeSelection = false;
            int stableChecks = 0;
            string streamIndexLabel = targetStream != null
                ? targetStream.Index.ToString(CultureInfo.InvariantCulture)
                : "-";

            if (DebugSwitches.EnablePlaybackDebugOverlay)
            {
                CaptureStreamDebugEvent(
                    "StartPlayback.ParserNativeOffWatchdog",
                    $"stream={streamIndexLabel},windowMs={stabilizeWindowMs},pollMs={stabilizePollMs}");
            }

            for (int tick = 1; tick <= maxTicks; tick++)
            {
                await Task.Delay(stabilizePollMs);
                if (playbackToken != _playbackToken || _player == null)
                    return true;

                int nativeCount = GetNativeSubtitleTrackCountSafe();
                int selectedNative = GetSelectedNativeSubtitleTrackSafe();
                bool nativeTrackActive = nativeCount > 0 && selectedNative >= 0;

                if (nativeTrackActive)
                {
                    observedNativeSelection = true;
                    stableChecks = 0;
                    TryDisableNativeSubtitleTrack();

                    int selectedAfterClear = GetSelectedNativeSubtitleTrackSafe();
                    if (DebugSwitches.EnablePlaybackDebugOverlay)
                    {
                        CaptureStreamDebugEvent(
                            "StartPlayback.ParserNativeOffClear",
                            $"stream={streamIndexLabel},tick={tick},before={selectedNative},after={selectedAfterClear},count={nativeCount}");
                        CaptureSubtitleDebugEvent(
                            "Subtitle.ParserNativeInterference",
                            targetStream,
                            $"tick={tick},before={selectedNative},after={selectedAfterClear},count={nativeCount}");
                    }

                    continue;
                }

                stableChecks++;
                if (stableChecks >= stableChecksRequired)
                {
                    if (DebugSwitches.EnablePlaybackDebugOverlay)
                    {
                        CaptureStreamDebugEvent(
                            "StartPlayback.ParserNativeOffComplete",
                            $"stream={streamIndexLabel},tick={tick},result=stable,interference={(observedNativeSelection ? "yes" : "no")}");
                    }

                    return true;
                }
            }

            bool successAfterWindow = !observedNativeSelection || stableChecks > 0;
            if (DebugSwitches.EnablePlaybackDebugOverlay)
            {
                CaptureStreamDebugEvent(
                    "StartPlayback.ParserNativeOffComplete",
                    $"stream={streamIndexLabel},result={(successAfterWindow ? "ok" : "retry")},interference={(observedNativeSelection ? "yes" : "no")},stableChecks={stableChecks}");
                CaptureSubtitleDebugEvent(
                    "Subtitle.ParserNativeCheck",
                    targetStream,
                    $"windowMs={stabilizeWindowMs},interference={(observedNativeSelection ? "yes" : "no")},stableChecks={stableChecks},result={(successAfterWindow ? "ok" : "retry")}");
            }

            return successAfterWindow;
        }

        private int GetNativeSubtitleTrackCountSafe()
        {
            try
            {
                return _player?.SubtitleTrackInfo.GetCount() ?? -1;
            }
            catch
            {
                return -1;
            }
        }

        private int GetSelectedNativeSubtitleTrackSafe()
        {
            try
            {
                return _player?.SubtitleTrackInfo.Selected ?? -2;
            }
            catch
            {
                return -2;
            }
        }

        private bool TryLoadAssSubtitleCues(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;

                var cues = new List<SubtitleCue>();
                bool inEvents = false;
                int formatCount = 0;
                int startIndex = 1;
                int endIndex = 2;
                int textIndex = 9;

                foreach (var rawLine in File.ReadLines(path))
                {
                    string line = rawLine?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith(";", StringComparison.Ordinal))
                        continue;

                    if (line.StartsWith("[", StringComparison.Ordinal))
                    {
                        inEvents = line.Equals("[Events]", StringComparison.OrdinalIgnoreCase);
                        continue;
                    }

                    if (!inEvents)
                        continue;

                    if (line.StartsWith("Format:", StringComparison.OrdinalIgnoreCase))
                    {
                        var columns = line.Substring("Format:".Length)
                            .Split(',')
                            .Select(c => c.Trim())
                            .Where(c => !string.IsNullOrWhiteSpace(c))
                            .ToList();

                        formatCount = columns.Count;
                        if (formatCount > 0)
                        {
                            int s = columns.FindIndex(c => c.Equals("Start", StringComparison.OrdinalIgnoreCase));
                            int e = columns.FindIndex(c => c.Equals("End", StringComparison.OrdinalIgnoreCase));
                            int t = columns.FindIndex(c => c.Equals("Text", StringComparison.OrdinalIgnoreCase));
                            if (s >= 0) startIndex = s;
                            if (e >= 0) endIndex = e;
                            if (t >= 0) textIndex = t;
                        }
                        continue;
                    }

                    if (!line.StartsWith("Dialogue:", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string payload = line.Substring("Dialogue:".Length).TrimStart();
                    int expectedColumns = formatCount > 0 ? formatCount : 10;
                    var fields = SplitAssDialogueColumns(payload, expectedColumns);
                    int minRequiredIndex = Math.Max(textIndex, Math.Max(startIndex, endIndex));
                    if (fields.Count <= minRequiredIndex)
                        continue;

                    if (!TryParseAssSubtitleTimestamp(fields[startIndex], out int startMs))
                        continue;
                    if (!TryParseAssSubtitleTimestamp(fields[endIndex], out int endMs))
                        continue;
                    if (endMs <= startMs) endMs = startMs + 500;

                    string cueText = NormalizeParsedCueText(fields[textIndex]);
                    if (string.IsNullOrWhiteSpace(cueText))
                        continue;

                    cues.Add(new SubtitleCue
                    {
                        StartMs = startMs,
                        EndMs = endMs,
                        Text = cueText
                    });
                }

                SetParsedSubtitleCues(cues);
                return _subtitleCues.Count > 0;
            }
            catch
            {
                ClearParsedSubtitleCues();
                return false;
            }
        }

        private static List<string> SplitAssDialogueColumns(string payload, int expectedColumns)
        {
            var result = new List<string>();
            if (payload == null)
            {
                result.Add(string.Empty);
                return result;
            }

            if (expectedColumns <= 1)
            {
                result.Add(payload);
                return result;
            }

            int start = 0;
            for (int i = 0; i < payload.Length && result.Count < expectedColumns - 1; i++)
            {
                if (payload[i] != ',')
                    continue;

                result.Add(payload.Substring(start, i - start));
                start = i + 1;
            }

            if (start <= payload.Length)
                result.Add(payload.Substring(start));

            return result;
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
                        Text = NormalizeParsedCueText(cueText)
                    });
                }

                SetParsedSubtitleCues(cues);
                return _subtitleCues.Count > 0;
            }
            catch
            {
                ClearParsedSubtitleCues();
                return false;
            }
        }

        private bool TryParseAssSubtitleTimestamp(string input, out int totalMs)
        {
            totalMs = 0;
            if (string.IsNullOrWhiteSpace(input)) return false;

            string token = input.Trim();
            int spaceIndex = token.IndexOf(' ');
            if (spaceIndex >= 0) token = token.Substring(0, spaceIndex);

            string[] parts = token.Split(':');
            if (parts.Length != 3) return false;

            if (!int.TryParse(parts[0], out int hours)) return false;
            if (!int.TryParse(parts[1], out int minutes)) return false;

            string secondToken = parts[2].Trim();
            int seconds;
            int millis = 0;

            int dotIndex = secondToken.IndexOf('.');
            if (dotIndex < 0)
                dotIndex = secondToken.IndexOf(',');

            if (dotIndex >= 0)
            {
                string secPart = secondToken.Substring(0, dotIndex);
                string fraction = secondToken.Substring(dotIndex + 1);
                if (!int.TryParse(secPart, out seconds)) return false;

                string msPart = fraction.Trim();
                if (msPart.Length > 3) msPart = msPart.Substring(0, 3);
                while (msPart.Length < 3) msPart += "0";
                if (!int.TryParse(msPart, out millis)) return false;
            }
            else
            {
                if (!int.TryParse(secondToken, out seconds)) return false;
            }

            totalMs = (((hours * 60) + minutes) * 60 + seconds) * 1000 + millis;
            return true;
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
            _subtitleHideDeadlineUtc = DateTime.MinValue;
            _activeSubtitleCueIndex = -1;
            _activeParsedSubtitleText = string.Empty;
            _subtitleRenderTimer = new Timer(ParsedSubtitleRenderTickMs);
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

        private int FindParsedSubtitleCueIndex(int queryPosMs)
        {
            int count = _subtitleCues.Count;
            if (count == 0)
                return -1;

            if (_subtitleCuePrefixMaxEnd.Length != count)
                RebuildParsedCuePrefixMaxEnd();

            int upperBound = FindLastCueStartAtOrBefore(queryPosMs);
            if (upperBound < 0)
                return -1;

            if (_subtitleCuePrefixMaxEnd[upperBound] < queryPosMs)
                return -1;

            int low = 0;
            int high = upperBound;
            while (low < high)
            {
                int mid = low + ((high - low) / 2);
                if (_subtitleCuePrefixMaxEnd[mid] >= queryPosMs)
                    high = mid;
                else
                    low = mid + 1;
            }

            for (int i = low; i <= upperBound; i++)
            {
                if (_subtitleCues[i].EndMs >= queryPosMs)
                    return i;
            }

            return -1;
        }

        private int FindLastCueStartAtOrBefore(int queryPosMs)
        {
            int low = 0;
            int high = _subtitleCues.Count - 1;
            int answer = -1;

            while (low <= high)
            {
                int mid = low + ((high - low) / 2);
                if (_subtitleCues[mid].StartMs <= queryPosMs)
                {
                    answer = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return answer;
        }

        private void UpdateParsedSubtitleRender()
        {
            if (!_useParsedSubtitleRenderer || _subtitleText == null || _player == null || !_subtitleEnabled || _subtitleCues.Count == 0)
            {
                _subtitleText?.Hide();
                _activeSubtitleCueIndex = -1;
                _activeParsedSubtitleText = string.Empty;
                return;
            }

            int queryPosMs = GetPlayPositionMs() - _subtitleOffsetMs;
            if (queryPosMs < 0)
            {
                _subtitleText.Hide();
                _activeSubtitleCueIndex = -1;
                _activeParsedSubtitleText = string.Empty;
                return;
            }

            int firstCandidate = FindParsedSubtitleCueIndex(queryPosMs);
            if (firstCandidate == -1)
            {
                if (_activeSubtitleCueIndex != -1) _subtitleText.Hide();
                _activeSubtitleCueIndex = -1;
                _activeParsedSubtitleText = string.Empty;
                return;
            }

            int firstActiveIndex = -1;
            var activeLines = new List<string>();
            for (int i = firstCandidate; i < _subtitleCues.Count; i++)
            {
                var cue = _subtitleCues[i];
                if (cue.StartMs > queryPosMs)
                    break;

                if (queryPosMs >= cue.StartMs && queryPosMs <= cue.EndMs)
                {
                    if (firstActiveIndex < 0)
                        firstActiveIndex = i;

                    if (!string.IsNullOrWhiteSpace(cue.Text))
                        activeLines.Add(cue.Text);
                }
            }

            if (firstActiveIndex < 0 || activeLines.Count == 0)
            {
                _subtitleText.Hide();
                _activeSubtitleCueIndex = -1;
                _activeParsedSubtitleText = string.Empty;
                return;
            }

            string mergedText = string.Join("\n", activeLines).Trim();
            if (string.IsNullOrWhiteSpace(mergedText))
            {
                _subtitleText.Hide();
                _activeSubtitleCueIndex = -1;
                _activeParsedSubtitleText = string.Empty;
                return;
            }

            if (!string.Equals(mergedText, _activeParsedSubtitleText, StringComparison.Ordinal))
            {
                _subtitleText.Text = mergedText;
                _activeParsedSubtitleText = mergedText;
            }

            _activeSubtitleCueIndex = firstActiveIndex;
            _subtitleText.Show();
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
                try { _player.ClearSubtitle(); } catch { }
                if (_player.SubtitleTrackInfo.GetCount() > 0) _player.SubtitleTrackInfo.Selected = -1;
            }
            catch { }
        }

        private void ApplyDisplayModeForCurrentVideo()
        {
            if (_player == null)
                return;

            if (_useFullscreenAspectMode && _isAspectToggleVisible)
                _player.DisplaySettings.Mode = ResolveFullscreenDisplayMode();
            else
                _player.DisplaySettings.Mode = PlayerDisplayMode.LetterBox;
        }

        private static PlayerDisplayMode ResolveFullscreenDisplayMode()
        {
            if (TryParseDisplayMode("FullScreen", out var mode))
                return mode;
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

        private async Task RefreshAnamorphicStateAsync(string itemId, int playbackToken)
        {
            bool isAnamorphic = false;
            try
            {
                isAnamorphic = await AppState.Jellyfin.GetIsAnamorphicAsync(itemId);
            }
            catch
            {
                isAnamorphic = false;
            }

            if (playbackToken != _playbackToken)
                return;

            RunOnUiThread(() =>
            {
                if (playbackToken != _playbackToken)
                    return;

                _isAnamorphicVideo = isAnamorphic;
                SetAspectToggleVisibility(_isAnamorphicVideo);
                ApplyDisplayModeForCurrentVideo();
            });
        }

        private int GetAspectButtonIndex()
        {
            if (!_isAspectToggleVisible)
                return -1;

            return _movie.ItemType == "Episode" ? 3 : 2;
        }

        private void SetAspectToggleVisibility(bool visible)
        {
            _isAspectToggleVisible = visible;
            if (!_isAspectToggleVisible)
                _useFullscreenAspectMode = false;

            int baseCount = _movie.ItemType == "Episode" ? 3 : 2;
            _osdButtonCount = baseCount + (_isAspectToggleVisible ? 1 : 0);
            _buttonFocusIndex = Math.Clamp(_buttonFocusIndex, 0, Math.Max(0, _osdButtonCount - 1));

            if (_controlsContainer == null || _aspectButton == null)
                return;

            bool added = false;
            foreach (var child in _controlsContainer.Children)
            {
                if (ReferenceEquals(child, _aspectButton))
                {
                    added = true;
                    break;
                }
            }

            if (visible && !added)
                _controlsContainer.Add(_aspectButton);
            else if (!visible && added)
                _controlsContainer.Remove(_aspectButton);

            UpdateAspectButtonText();
            UpdateOsdFocus();
        }

        private void ToggleAspectMode()
        {
            if (!_isAspectToggleVisible)
                return;

            _useFullscreenAspectMode = !_useFullscreenAspectMode;
            ApplyDisplayModeForCurrentVideo();
            UpdateAspectButtonText();
            UpdateOsdFocus();
            _osdTimer.Stop();
            _osdTimer.Start();
        }

        private void UpdateAspectButtonText()
        {
            string modeText = _useFullscreenAspectMode ? "Aspect: Fullscreen" : "Aspect: Letterbox";
            SetOsdButtonText(_aspectButton, modeText);
        }

        private void CreateOSD()
        {
            int sidePadding = TopOsdSidePadding; int labelWidth = 140; int labelGap = 20; int bottomHeight = 260; int topHeight = 160; int screenWidth = Window.Default.Size.Width;
            _topOsdShownY = 0;
            _topOsdHiddenY = -OsdSlideDistance;
            _osdShownY = Window.Default.Size.Height - bottomHeight;
            _osdHiddenY = _osdShownY + OsdSlideDistance;

            _topOsd = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = topHeight,
                PositionY = _topOsdHiddenY,
                Opacity = 0.0f
            };
            var topGradient = new PropertyMap();
            topGradient.Add(Visual.Property.Type, new PropertyValue((int)Visual.Type.Gradient));
            topGradient.Add(GradientVisualProperty.StartPosition, new PropertyValue(new Vector2(0.0f, -0.5f)));
            topGradient.Add(GradientVisualProperty.EndPosition, new PropertyValue(new Vector2(0.0f, 0.5f)));
            var topOffsets = new PropertyArray();
            topOffsets.Add(new PropertyValue(0.0f));
            topOffsets.Add(new PropertyValue(1.0f));
            topGradient.Add(GradientVisualProperty.StopOffset, new PropertyValue(topOffsets));
            var topColors = new PropertyArray();
            topColors.Add(new PropertyValue(UiTheme.PlayerTopGradientStart));
            topColors.Add(new PropertyValue(UiTheme.PlayerTopGradientEnd));
            topGradient.Add(GradientVisualProperty.StopColor, new PropertyValue(topColors));
            _topOsd.Background = topGradient;
            _topOsd.Hide();

            _topOsdTitleView = CreateTopOsdTitleView(sidePadding);
            _topOsd.Add(_topOsdTitleView);
            _clockLabel = new TextLabel(FormatClockTime(DateTime.Now))
            {
                PositionX = screenWidth - sidePadding - 180,
                PositionY = 40,
                WidthSpecification = UiTheme.PlayerClockBoxWidth,
                HeightSpecification = UiTheme.PlayerClockBoxHeight,
                PointSize = UiTheme.PlayerClockText,
                TextColor = Color.White,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.End
            };
            _topOsd.Add(_clockLabel);
            Add(_topOsd);

            _osd = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = bottomHeight,
                PositionY = _osdHiddenY,
                Opacity = 0.0f
            };
            var bottomGradient = new PropertyMap();
            bottomGradient.Add(Visual.Property.Type, new PropertyValue((int)Visual.Type.Gradient));
            bottomGradient.Add(GradientVisualProperty.StartPosition, new PropertyValue(new Vector2(0.0f, -0.5f)));
            bottomGradient.Add(GradientVisualProperty.EndPosition, new PropertyValue(new Vector2(0.0f, 0.5f)));
            var bottomOffsets = new PropertyArray();
            bottomOffsets.Add(new PropertyValue(0.0f));
            bottomOffsets.Add(new PropertyValue(1.0f));
            bottomGradient.Add(GradientVisualProperty.StopOffset, new PropertyValue(bottomOffsets));
            var bottomColors = new PropertyArray();
            bottomColors.Add(new PropertyValue(UiTheme.PlayerBottomGradientStart));
            bottomColors.Add(new PropertyValue(UiTheme.PlayerBottomGradientEnd));
            bottomGradient.Add(GradientVisualProperty.StopColor, new PropertyValue(bottomColors));
            _osd.Background = bottomGradient;
            _osd.Hide();

            var progressRow = new View { WidthResizePolicy = ResizePolicyType.FillToParent, HeightSpecification = UiTheme.PlayerProgressRowHeight, PositionY = UiTheme.PlayerProgressRowY };
            _currentTimeLabel = new TextLabel("00:00") { PositionX = sidePadding, WidthSpecification = labelWidth, HeightResizePolicy = ResizePolicyType.FillToParent, PointSize = UiTheme.PlayerTimeTextSize, TextColor = Color.White, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Begin };
            _durationLabel = new TextLabel("00:00") { PositionX = screenWidth - sidePadding - labelWidth, WidthSpecification = labelWidth, HeightResizePolicy = ResizePolicyType.FillToParent, PointSize = UiTheme.PlayerTimeTextSize, TextColor = Color.White, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.End };

            int trackStartX = sidePadding + labelWidth + labelGap;
            int trackWidth = screenWidth - (2 * trackStartX);

            _progressTrack = new View { PositionX = trackStartX, WidthSpecification = trackWidth, HeightSpecification = 6, BackgroundColor = UiTheme.PlayerTrackBase, PositionY = 22, CornerRadius = 3.0f };
            _progressFill = new View { HeightSpecification = 6, BackgroundColor = Color.White, WidthSpecification = 0, CornerRadius = 3.0f };
            _previewFill = new View { HeightSpecification = 6, BackgroundColor = UiTheme.PlayerPreviewFill, WidthSpecification = 0, CornerRadius = 3.0f };
            _progressTrack.Add(_progressFill);
            _progressTrack.Add(_previewFill);

            _progressThumb = new View { WidthSpecification = 24, HeightSpecification = 24, BackgroundColor = Color.White, CornerRadius = 12.0f, PositionY = 13, PositionX = trackStartX };
            _endsAtLabel = new TextLabel("")
            {
                PositionX = trackStartX,
                PositionY = 40,
                WidthSpecification = trackWidth,
                HeightSpecification = UiTheme.PlayerEndsAtHeight,
                PointSize = UiTheme.PlayerEndsAtText,
                TextColor = UiTheme.PlayerTimeText,
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
                HeightSpecification = UiTheme.PlayerControlsRowHeight,
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
            _aspectButton = CreateOsdButton(null, "Aspect: Letterbox", 280);

            _controlsContainer.Add(_audioButton);
            _controlsContainer.Add(_subtitleButton);
            _buttonFocusIndex = AudioButtonIndex;

            if (_movie.ItemType == "Episode")
            {
                _controlsContainer.Add(_nextButton);
            }
            SetAspectToggleVisibility(_isAspectToggleVisible);

            CreateSubtitleOffsetTrack(screenWidth);
            UpdateSubtitleOffsetUI();

            _osd.Add(progressRow);
            _osd.Add(_subtitleOffsetTrackContainer);
            _osd.Add(_controlsContainer);
            Add(_osd);
            if (_trickplayPreviewContainer == null) CreateTrickplayPreview();
            if (_seekFeedbackContainer == null) CreateSeekFeedback();
            if (_playPauseFeedbackContainer == null) CreatePlayPauseFeedback();
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

        private MediaSourceInfo ResolvePreferredMediaSource(PlaybackInfoResponse playbackInfo)
        {
            if (playbackInfo?.MediaSources == null || playbackInfo.MediaSources.Count == 0)
                return null;

            var mediaSource = playbackInfo.MediaSources[0];
            if (!string.IsNullOrWhiteSpace(_preferredMediaSourceId))
            {
                var preferredSource = playbackInfo.MediaSources.FirstOrDefault(s => s.Id == _preferredMediaSourceId);
                if (preferredSource != null)
                    mediaSource = preferredSource;
            }

            return mediaSource;
        }

        private static string ResolveReportedPlayMethod(string routingPlayMethod, MediaSourceInfo mediaSource, bool requiresServerManagedStream)
        {
            if (string.Equals(routingPlayMethod, "DirectPlay", StringComparison.OrdinalIgnoreCase))
                return "DirectPlay";

            if (mediaSource?.SupportsDirectStream == true)
                return "DirectStream";

            if (string.Equals(routingPlayMethod, "Transcode", StringComparison.OrdinalIgnoreCase) &&
                !requiresServerManagedStream &&
                LooksLikeDirectStream(mediaSource))
            {
                return "DirectStream";
            }

            return string.IsNullOrWhiteSpace(routingPlayMethod) ? "DirectPlay" : routingPlayMethod;
        }

        private static bool LooksLikeDirectStream(MediaSourceInfo mediaSource)
        {
            if (mediaSource == null || string.IsNullOrWhiteSpace(mediaSource.TranscodingUrl))
                return false;

            if (HasHardTranscodeIndicators(mediaSource))
                return false;

            string originalVideoCodec = NormalizeVideoCodec(
                mediaSource.MediaStreams?.FirstOrDefault(s => string.Equals(s.Type, "Video", StringComparison.OrdinalIgnoreCase))?.Codec);
            if (string.IsNullOrWhiteSpace(originalVideoCodec))
                return false;

            string requestedVideoCodecs = GetQueryParamValue(mediaSource.TranscodingUrl, "VideoCodec");
            if (string.IsNullOrWhiteSpace(requestedVideoCodecs))
                return false;

            var codecs = requestedVideoCodecs.Split(',');
            for (int i = 0; i < codecs.Length; i++)
            {
                if (string.Equals(NormalizeVideoCodec(codecs[i]), originalVideoCodec, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool HasHardTranscodeIndicators(MediaSourceInfo mediaSource)
        {
            if (mediaSource == null)
                return false;

            string subtitleMethod = GetQueryParamValue(mediaSource.TranscodingUrl, "SubtitleMethod");
            if (string.Equals(subtitleMethod, "Encode", StringComparison.OrdinalIgnoreCase))
            {
                string subtitleStreamIndex = GetQueryParamValue(mediaSource.TranscodingUrl, "SubtitleStreamIndex");
                if (!string.IsNullOrWhiteSpace(subtitleStreamIndex))
                    return true;
            }

            var reasons = mediaSource.TranscodingReasons;
            if (reasons == null || reasons.Count == 0)
                return false;

            foreach (var reason in reasons)
            {
                if (string.IsNullOrWhiteSpace(reason))
                    continue;

                string normalized = reason.Trim().ToLowerInvariant();
                if (normalized.Contains("video") ||
                    normalized.Contains("subtitle") ||
                    normalized.Contains("anamorphic") ||
                    normalized.Contains("interlace") ||
                    normalized.Contains("bitdepth") ||
                    normalized.Contains("framerate") ||
                    normalized.Contains("level"))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeVideoCodec(string codec)
        {
            if (string.IsNullOrWhiteSpace(codec))
                return string.Empty;

            string normalized = codec.Trim().ToLowerInvariant();
            return normalized switch
            {
                "h265" => "hevc",
                "x265" => "hevc",
                "x264" => "h264",
                _ => normalized
            };
        }

        private static string GetQueryParamValue(string url, string key)
        {
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key))
                return string.Empty;

            int queryStart = url.IndexOf('?');
            if (queryStart < 0 || queryStart >= url.Length - 1)
                return string.Empty;

            string query = url.Substring(queryStart + 1);
            var parts = query.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                int equalsIndex = part.IndexOf('=');
                if (equalsIndex <= 0)
                    continue;

                string candidateKey = part.Substring(0, equalsIndex);
                if (!string.Equals(candidateKey, key, StringComparison.OrdinalIgnoreCase))
                    continue;

                string rawValue = part.Substring(equalsIndex + 1);
                return Uri.UnescapeDataString(rawValue);
            }

            return string.Empty;
        }

        private static int? ResolveStartupDefaultAudioIndex(MediaSourceInfo mediaSource)
        {
            var audioStreams = mediaSource?.MediaStreams?
                .Where(s => string.Equals(s.Type, "Audio", StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.Index)
                .ToList();
            if (audioStreams == null || audioStreams.Count == 0)
                return null;

            var defaultStream = audioStreams.FirstOrDefault(s => s.IsDefault);
            return defaultStream?.Index ?? audioStreams[0].Index;
        }

        private static bool IsAudioCodecDirectPlayableByProfile(string codec)
        {
            if (string.IsNullOrWhiteSpace(codec))
                return false;

            string normalized = NormalizeAudioCodec(codec);
            if (string.IsNullOrWhiteSpace(normalized))
                return false;

            var profile = ProfileBuilder.BuildTizenProfile(forceBurnIn: false, disableSubtitles: false);
            var directPlayProfiles = profile?.DirectPlayProfiles;
            if (directPlayProfiles == null || directPlayProfiles.Count == 0)
                return false;

            foreach (var profileEntry in directPlayProfiles)
            {
                if (string.IsNullOrWhiteSpace(profileEntry?.AudioCodec))
                    continue;

                var codecs = profileEntry.AudioCodec.Split(',');
                for (int i = 0; i < codecs.Length; i++)
                {
                    string candidate = NormalizeAudioCodec(codecs[i]);
                    if (!string.IsNullOrWhiteSpace(candidate) &&
                        string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string NormalizeAudioCodec(string codec)
        {
            if (string.IsNullOrWhiteSpace(codec))
                return string.Empty;

            string normalized = codec.Trim().ToLowerInvariant();
            return normalized switch
            {
                "e-ac-3" => "eac3",
                "eac-3" => "eac3",
                "ac-3" => "ac3",
                "dca" => "dts",
                "mp4a" => "aac",
                _ => normalized
            };
        }

        private List<MediaStream> GetOrderedAudioStreams(MediaSourceInfo mediaSource = null)
        {
            var source = mediaSource ?? _currentMediaSource;
            return source?.MediaStreams?
                .Where(s => string.Equals(s.Type, "Audio", StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.Index)
                .ToList();
        }

        private static List<MediaStream> GetDirectPlayableAudioStreams(List<MediaStream> audioStreams)
        {
            if (audioStreams == null || audioStreams.Count == 0)
                return new List<MediaStream>();

            return audioStreams
                .Where(s => IsAudioCodecDirectPlayableByProfile(s?.Codec))
                .OrderBy(s => s.Index)
                .ToList();
        }

        private void SetAudioIndexByStreamIndex(List<MediaStream> audioStreams, int jellyfinStreamIndex)
        {
            if (audioStreams == null || audioStreams.Count == 0)
            {
                _audioIndex = 0;
                return;
            }

            int matchIndex = audioStreams.FindIndex(s => s.Index == jellyfinStreamIndex);
            _audioIndex = matchIndex >= 0
                ? matchIndex
                : Math.Clamp(_audioIndex, 0, audioStreams.Count - 1);
        }

        private bool TrySelectNativeAudioTrack(int jellyfinStreamIndex)
        {
            if (_player == null || _currentMediaSource == null)
                return false;

            if (!string.Equals(_playMethod, "DirectPlay", StringComparison.OrdinalIgnoreCase))
                return false;

            try
            {
                int tizenAudioCount = _player.AudioTrackInfo.GetCount();
                if (tizenAudioCount <= 0)
                    return false;

                var audioStreams = GetOrderedAudioStreams();
                if (audioStreams == null || audioStreams.Count == 0)
                    return false;

                var directPlayableStreams = GetDirectPlayableAudioStreams(audioStreams);
                if (directPlayableStreams.Count == 0)
                    return false;

                int tizenIndex = directPlayableStreams.FindIndex(s => s.Index == jellyfinStreamIndex);
                if (tizenIndex < 0 || tizenIndex >= tizenAudioCount)
                    return false;

                _player.AudioTrackInfo.Selected = tizenIndex;
                _overrideAudioIndex = jellyfinStreamIndex;
                SetAudioIndexByStreamIndex(audioStreams, jellyfinStreamIndex);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void SyncAudioSelectionFromPlaybackState(List<MediaStream> audioStreams)
        {
            if (audioStreams == null || audioStreams.Count == 0)
            {
                _audioIndex = 0;
                return;
            }

            int selectedStreamIndex = -1;
            if (string.Equals(_playMethod, "DirectPlay", StringComparison.OrdinalIgnoreCase) && _player != null)
            {
                try
                {
                    int selectedTizenIndex = _player.AudioTrackInfo.Selected;
                    var directPlayableStreams = GetDirectPlayableAudioStreams(audioStreams);
                    if (selectedTizenIndex >= 0 && selectedTizenIndex < directPlayableStreams.Count)
                        selectedStreamIndex = directPlayableStreams[selectedTizenIndex].Index;
                }
                catch
                {
                }
            }

            if (selectedStreamIndex < 0 && _overrideAudioIndex.HasValue)
                selectedStreamIndex = _overrideAudioIndex.Value;

            if (selectedStreamIndex < 0)
                selectedStreamIndex = audioStreams[0].Index;

            SetAudioIndexByStreamIndex(audioStreams, selectedStreamIndex);
        }

        private void ApplyPendingNativeAudioOverride(int playbackToken, MediaSourceInfo mediaSource)
        {
            if (!_overrideAudioIndex.HasValue || mediaSource == null)
                return;

            if (!string.Equals(_playMethod, "DirectPlay", StringComparison.OrdinalIgnoreCase))
                return;

            int requestedStreamIndex = _overrideAudioIndex.Value;
            if (TrySelectNativeAudioTrack(requestedStreamIndex))
                return;

            _ = RetryNativeAudioOverrideSelectionAsync(playbackToken, requestedStreamIndex);
        }

        private async Task RetryNativeAudioOverrideSelectionAsync(int playbackToken, int jellyfinStreamIndex)
        {
            await Task.Delay(140);
            if (playbackToken != _playbackToken)
                return;

            TrySelectNativeAudioTrack(jellyfinStreamIndex);
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
                PointSize = UiTheme.PlayerTopTitleText,
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
                HeightSpecification = UiTheme.PlayerControlButtonHeight,
                BackgroundColor = Color.Black,
                BorderlineWidth = 2.0f,
                BorderlineColor = Color.White,
                CornerRadius = UiTheme.PlayerControlButtonRadius,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                ClippingMode = ClippingModeType.ClipChildren
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

            if (!string.IsNullOrWhiteSpace(iconFile))
            {
                var icon = new ImageView
                {
                    WidthSpecification = UiTheme.PlayerControlIconSize,
                    HeightSpecification = UiTheme.PlayerControlIconSize,
                    ResourceUrl = ResolveFreshIconPath(iconFile),
                    Name = iconFile,
                    FittingMode = FittingModeType.ShrinkToFit,
                    SamplingMode = SamplingModeType.BoxThenLanczos
                };
                content.Add(icon);
            }

            var label = new TextLabel(text)
            {
                HeightResizePolicy = ResizePolicyType.FillToParent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextColor = Color.White,
                PointSize = UiTheme.PlayerControlLabelText
            };

            content.Add(label);
            btn.Add(content);
            UiFactory.SetButtonFocusState(btn, focused: false);
            ApplyOsdButtonIconState(btn, focused: false);
            return btn;
        }

        private string ResolveFreshIconPath(string iconFile)
        {
            if (string.IsNullOrWhiteSpace(iconFile))
                return string.Empty;

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
                BackgroundColor = Color.Transparent,
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
                PointSize = UiTheme.PlayerSeekFeedbackText,
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

            _seekFeedbackIcon.ResourceUrl = ResolveFreshIconPath(direction > 0 ? "forward.svg" : "reverse.svg");
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
                BackgroundColor = Color.Transparent,
                CornerRadius = 70.0f,
                Opacity = 0.0f,
                Scale = new Vector3(0.9f, 0.9f, 1f)
            };

            _playFeedbackIcon = new ImageView
            {
                WidthSpecification = 110,
                HeightSpecification = 110,
                PositionX = 15,
                PositionY = 15,
                ResourceUrl = _sharedResPath + "play.svg",
                FittingMode = FittingModeType.ShrinkToFit,
                SamplingMode = SamplingModeType.BoxThenLanczos
            };

            _pauseFeedbackIcon = new ImageView
            {
                WidthSpecification = 110,
                HeightSpecification = 110,
                PositionX = 15,
                PositionY = 15,
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
                BackgroundColor = MonochromeAuthFactory.PanelFallbackColor,
                CornerRadius = 8.0f,
                BorderlineWidth = 2.0f,
                BorderlineColor = MonochromeAuthFactory.PanelFallbackBorder,
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
                PointSize = UiTheme.PlayerSmartPopupTitle,
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
                PointSize = UiTheme.PlayerSmartPopupSubtitle,
                TextColor = UiTheme.PlayerSubtitleText,
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
                Name = "next.svg",
                FittingMode = FittingModeType.ShrinkToFit,
                SamplingMode = SamplingModeType.BoxThenLanczos
            };

            content.Add(_smartActionTitleLabel);
            content.Add(_smartActionSubtitleLabel);
            content.Add(_smartActionIcon);
            _smartActionPopup.Add(content);
            UiFactory.SetButtonFocusState(_smartActionPopup, focused: false);
            ApplySmartPopupIconState(focused: false);
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

            UiFactory.SetButtonFocusState(_smartActionPopup, focused: _smartPopupFocused);
            ApplySmartPopupIconState(_smartPopupFocused);
            AnimateFocusScale(_smartActionPopup, _smartPopupFocused ? new Vector3(1.04f, 1.04f, 1f) : Vector3.One);
        }

        private void ApplySmartPopupIconState(bool focused)
        {
            if (_smartActionIcon == null)
                return;

            string iconFile = "next.svg";
            _smartActionIcon.Name = iconFile;
            _smartActionIcon.ResourceUrl = ResolveOsdIconPath(iconFile, focused);
        }

        private void ShowSmartPopup(string title, string subtitle, bool isIntro, bool focused)
        {
            // Smart popup is created as part of CreateOSD; ensure it exists even when
            // the user has never manually opened the OSD.
            EnsureOsdCreated();
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
                    RunOnUiThread(UpdateSmartActionTimerState);
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

                RunOnUiThread(UpdateSmartActionTimerState);
            }
            catch
            {
                _hasIntroSegment = false;
                _hasOutroSegment = false;
                RunOnUiThread(UpdateSmartActionTimerState);
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

        private void UpdateSmartActionTimerState()
        {
            bool hasSmartAction = _hasIntroSegment ||
                                  (_movie?.ItemType == "Episode" && _hasOutroSegment);
            if (_player == null || _isFinished || _isEpisodeSwitchInProgress || !hasSmartAction)
            {
                _smartActionTimer?.Stop();
                return;
            }

            _smartActionTimer ??= new Timer(SmartActionTickMs);
            _smartActionTimer.Stop();
            _smartActionTimer.Tick -= OnSmartActionTick;
            _smartActionTimer.Tick += OnSmartActionTick;
            _smartActionTimer.Start();
        }

        private void EvaluateSmartActions()
        {
            bool hasSmartAction = _hasIntroSegment ||
                                  (_movie?.ItemType == "Episode" && _hasOutroSegment);
            if (!hasSmartAction)
            {
                HideSmartPopup();
                _smartActionTimer?.Stop();
                return;
            }

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

        private void SkipIntro()
        {
            FireAndForget(SkipIntroAsync());
        }

        private async Task SkipIntroAsync()
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
            UpdateSmartActionTimerState();
            HideSmartPopup();
        }

        private void SetButtonVisual(View button, bool focused)
        {
            if (button == null)
                return;

            UiFactory.SetButtonFocusState(button, focused: focused);
            ApplyOsdButtonIconState(button, focused);
            AnimateFocusScale(button, focused ? new Vector3(1.08f, 1.08f, 1f) : Vector3.One);
        }

        private void ApplyOsdButtonIconState(View view, bool focused)
        {
            if (view == null)
                return;

            if (view is ImageView icon && !string.IsNullOrWhiteSpace(icon.Name))
                icon.ResourceUrl = ResolveOsdIconPath(icon.Name, focused);

            uint childCount = view.ChildCount;
            for (uint i = 0; i < childCount; i++)
            {
                if (view.GetChildAt(i) is View child)
                    ApplyOsdButtonIconState(child, focused);
            }
        }

        private string ResolveOsdIconPath(string iconFile, bool focused)
        {
            string sourcePath = ResolveFreshIconPath(iconFile);
            if (!focused ||
                string.IsNullOrWhiteSpace(sourcePath) ||
                !sourcePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(sourcePath))
            {
                return sourcePath;
            }

            string versionToken;
            try
            {
                versionToken = File.GetLastWriteTimeUtc(sourcePath)
                    .Ticks
                    .ToString(CultureInfo.InvariantCulture);
            }
            catch
            {
                versionToken = "0";
            }

            string cacheKey = $"{sourcePath}|{versionToken}";
            if (_darkOsdIconPathCache.TryGetValue(cacheKey, out var cachedPath) && File.Exists(cachedPath))
                return cachedPath;

            try
            {
                string svg = File.ReadAllText(sourcePath);
                string darkSvg = svg
                    .Replace("#FFFFFF", "#000000", StringComparison.OrdinalIgnoreCase)
                    .Replace("fill=\"white\"", "fill=\"#000000\"", StringComparison.OrdinalIgnoreCase)
                    .Replace("stroke=\"#FFFFFF\"", "stroke=\"#000000\"", StringComparison.OrdinalIgnoreCase)
                    .Replace("stroke=\"white\"", "stroke=\"#000000\"", StringComparison.OrdinalIgnoreCase);

                string cacheDir = System.IO.Path.Combine(Application.Current.DirectoryInfo.Data, "icon-cache");
                Directory.CreateDirectory(cacheDir);

                string baseName = System.IO.Path.GetFileNameWithoutExtension(iconFile);
                string darkPath = System.IO.Path.Combine(cacheDir, $"{baseName}_dark_{versionToken}.svg");
                if (!File.Exists(darkPath))
                    File.WriteAllText(darkPath, darkSvg);

                _darkOsdIconPathCache[cacheKey] = darkPath;
                return darkPath;
            }
            catch
            {
                return sourcePath;
            }
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
                HeightSpecification = UiTheme.PlayerSubtitleOffsetTrackHeight,
                PositionX = trackX,
                PositionY = 138
            };

            var trackLine = new View
            {
                WidthSpecification = SubtitleOffsetTrackWidth,
                HeightSpecification = UiTheme.PlayerSubtitleOffsetLineHeight,
                PositionY = UiTheme.PlayerSubtitleOffsetLineY,
                BackgroundColor = UiTheme.PlayerSubtitleOffsetTrack,
                CornerRadius = UiTheme.PlayerSubtitleOffsetLineRadius
            };

            _subtitleOffsetCenterMarker = new View
            {
                WidthSpecification = UiTheme.PlayerSubtitleOffsetCenterWidth,
                HeightSpecification = UiTheme.PlayerSubtitleOffsetCenterHeight,
                PositionX = (SubtitleOffsetTrackWidth / 2) - 1,
                PositionY = UiTheme.PlayerSubtitleOffsetCenterY,
                BackgroundColor = UiTheme.PlayerSubtitleOffsetCenter
            };

            _subtitleOffsetThumb = new View
            {
                WidthSpecification = UiTheme.PlayerSubtitleOffsetThumbSize,
                HeightSpecification = UiTheme.PlayerSubtitleOffsetThumbSize,
                PositionY = UiTheme.PlayerSubtitleOffsetThumbY,
                BackgroundColor = Color.White,
                CornerRadius = UiTheme.PlayerSubtitleOffsetThumbRadius
            };

            _subtitleOffsetTrackContainer.Add(trackLine);
            _subtitleOffsetTrackContainer.Add(_subtitleOffsetCenterMarker);
            _subtitleOffsetTrackContainer.Add(_subtitleOffsetThumb);
            _subtitleOffsetTrackContainer.Hide();
        }

        private void SetOsdButtonText(View button, string text)
        {
            if (button == null)
                return;

            var label = FindFirstTextLabel(button);
            if (label != null)
                label.Text = text;
        }

        private static TextLabel FindFirstTextLabel(View view)
        {
            if (view == null)
                return null;

            if (view is TextLabel directLabel)
                return directLabel;

            uint childCount = view.ChildCount;
            for (uint i = 0; i < childCount; i++)
            {
                if (view.GetChildAt(i) is not View child)
                    continue;

                var nested = FindFirstTextLabel(child);
                if (nested != null)
                    return nested;
            }

            return null;
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

            if (_subtitleOffsetButton != null)
            {
                UiFactory.SetButtonFocusState(_subtitleOffsetButton, focused: _subtitleOffsetAdjustMode);
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
            if (_subtitleOffsetButton != null)
            {
                UiFactory.SetButtonFocusState(_subtitleOffsetButton, focused: false);
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
            _subtitleOverlay = new View
            {
                WidthSpecification = UiTheme.PlayerOverlayWidth,
                HeightSpecification = UiTheme.PlayerOverlayHeight,
                BackgroundColor = MonochromeAuthFactory.PanelFallbackColor,
                PositionX = _subtitleOverlayBaseX + OverlaySlideDistance,
                PositionY = Window.Default.Size.Height - 780,
                CornerRadius = MonochromeAuthFactory.PanelCornerRadius,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BorderlineWidth = MonochromeAuthFactory.PanelBorderWidth,
                BorderlineColor = MonochromeAuthFactory.PanelFallbackBorder,
                ClippingMode = ClippingModeType.ClipChildren,
                Opacity = 0.0f
            };
            _subtitleOverlay.Hide();
            Add(_subtitleOverlay);
        }

        private void CreateSubtitleText()
        {
            if (_subtitleText != null)
            {
                ApplySubtitleTextStyle();
                return;
            }

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
            _subtitleText.PointSize = UiTheme.PlayerSubtitleTextSize;
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
            HidePlaybackInfoOverlay();
            ExitSubtitleOffsetAdjustMode();
            var wasVisible = _subtitleOverlayVisible;

            // 1. Use Jellyfin Metadata to list ALL tracks (fixes Tizen HLS limitation)
            var subtitleStreams = _currentMediaSource?.MediaStreams?
                .Where(s => s.Type == "Subtitle")
                .OrderBy(s => s.Index)
                .ToList();

            if (_subtitleOverlay.ChildCount == 0)
            {
                _subtitleOverlay.Add(new TextLabel("Subtitles") { WidthResizePolicy = ResizePolicyType.FillToParent, HeightSpecification = UiTheme.PlayerOverlayHeaderHeight, PointSize = UiTheme.PlayerOverlayHeader, TextColor = Color.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
                
                var offsetContainer = new View { WidthResizePolicy = ResizePolicyType.FillToParent, HeightSpecification = UiTheme.PlayerOverlayHeaderHeight, PositionY = UiTheme.PlayerOverlayHeaderHeight };
                _subtitleOffsetButton = new View
                {
                    WidthSpecification = UiTheme.PlayerSubtitleOffsetButtonWidth,
                    HeightSpecification = UiTheme.PlayerSubtitleOffsetButtonHeight,
                    BackgroundColor = Color.Black,
                    BorderlineWidth = 2.0f,
                    BorderlineColor = Color.White,
                    CornerRadius = UiTheme.PlayerSubtitleOffsetButtonRadius,
                    CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                    PositionX = UiTheme.PlayerSubtitleOffsetButtonX,
                    ClippingMode = ClippingModeType.ClipChildren
                };
                var offsetLabel = new TextLabel($"Offset: {FormatSubtitleOffsetLabel()}") { WidthResizePolicy = ResizePolicyType.FillToParent, HeightResizePolicy = ResizePolicyType.FillToParent, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextColor = Color.White, PointSize = UiTheme.PlayerOffsetLabel };
                _subtitleOffsetButton.Add(offsetLabel);
                UiFactory.SetButtonFocusState(_subtitleOffsetButton, focused: false);
                offsetContainer.Add(_subtitleOffsetButton);
                
                CreateSubtitleOffsetTrack(UiTheme.PlayerSubtitleOffsetButtonWidth);
                _subtitleOffsetTrackContainer.PositionX = UiTheme.PlayerSubtitleOffsetButtonX;
                _subtitleOffsetTrackContainer.PositionY = UiTheme.PlayerSubtitleOffsetTrackContainerY;
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
            }

            RebuildSubtitleRows(subtitleStreams);
            if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureSubtitleTopology("Subtitle.OverlayTopology", _currentMediaSource);

            SyncSubtitleSelectionFromCurrentState();
            UpdateSubtitleVisuals();
            ScrollSubtitleSelectionIntoView();
            int subtitleRowCount = _subtitleListContainer != null ? (int)_subtitleListContainer.ChildCount : 0;
            if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureStreamDebugEvent("Subtitle.OverlayShow", $"rows={subtitleRowCount},focus={_subtitleIndex}");
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
                WidthResizePolicy = ResizePolicyType.FillToParent, HeightSpecification = UiTheme.PlayerOverlayListRowHeight,
                BackgroundColor = Color.Black, CornerRadius = UiTheme.PlayerOverlayListRowHeight / 2.0f,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BorderlineWidth = 2.0f,
                BorderlineColor = Color.White,
                Margin = new Extents(UiTheme.PlayerOverlayItemMarginHorizontal, UiTheme.PlayerOverlayItemMarginHorizontal, UiTheme.PlayerOverlayItemMarginVertical, UiTheme.PlayerOverlayItemMarginVertical),
                Focusable = false,
                ClippingMode = ClippingModeType.ClipChildren
            };
            row.Name = indexId; 
            
            var label = new TextLabel(text) { WidthResizePolicy = ResizePolicyType.FillToParent, HeightResizePolicy = ResizePolicyType.FillToParent, PointSize = UiTheme.PlayerOverlayItem, TextColor = Color.White, HorizontalAlignment = HorizontalAlignment.Begin, VerticalAlignment = VerticalAlignment.Center, Padding = new Extents(UiTheme.PlayerOverlayItemPaddingLeft, (ushort)0, (ushort)0, (ushort)0) };
            row.Add(label);
            UiFactory.SetButtonFocusState(row, focused: false);
            return row;
        }

        private string DescribeSubtitleStream(MediaStream stream)
        {
            if (stream == null)
                return "none";

            string kind = stream.IsExternal ? "ext" : "emb";
            string lang = string.IsNullOrWhiteSpace(stream.Language) ? "-" : stream.Language.ToUpperInvariant();
            string codec = string.IsNullOrWhiteSpace(stream.Codec) ? "-" : stream.Codec.ToLowerInvariant();
            string deliveryExt = TryExtractSubtitleUrlExtension(stream.DeliveryUrl);
            return $"{stream.Index}:{kind}:{lang}:codec={codec}:deliveryExt={deliveryExt}";
        }

        private string GetNativeSubtitleState()
        {
            int count = -1;
            int selected = -2;
            int? expected = null;
            try
            {
                if (_player != null)
                    count = _player.SubtitleTrackInfo.GetCount();
            }
            catch
            {
            }

            try
            {
                if (_player != null)
                    selected = _player.SubtitleTrackInfo.Selected;
            }
            catch
            {
            }

            expected = GetExpectedNativeSubtitleSlot();
            string expectedLabel = expected.HasValue ? expected.Value.ToString(CultureInfo.InvariantCulture) : "-";
            return $"native={selected}/{count},expected={expectedLabel}";
        }

        private int? GetExpectedNativeSubtitleSlot()
        {
            if (_currentMediaSource == null || !_initialSubtitleIndex.HasValue || _initialSubtitleIndex.Value < 0)
                return null;

            var embedded = GetNativeSwitchableEmbeddedSubtitleStreams(_currentMediaSource);
            if (embedded == null || embedded.Count == 0)
                return null;

            var targetStream = embedded.FirstOrDefault(s => s.Index == _initialSubtitleIndex.Value);
            if (targetStream == null)
                return null;

            int nativeCount = -1;
            try
            {
                if (_player != null)
                    nativeCount = _player.SubtitleTrackInfo.GetCount();
            }
            catch
            {
            }

            if (nativeCount <= 0)
                return null;

            int resolvedSlot = ResolveNativeSubtitleSlotForStream(targetStream, embedded, nativeCount);
            return resolvedSlot >= 0 ? resolvedSlot : null;
        }

        private void CaptureSubtitleDebugEvent(string stage, MediaStream stream = null, string details = null)
        {
            if (!DebugSwitches.EnablePlaybackDebugOverlay)
                return;

            string requested = _initialSubtitleIndex.HasValue
                ? _initialSubtitleIndex.Value.ToString(CultureInfo.InvariantCulture)
                : "OFF";
            string state = $"req={requested},enabled={_subtitleEnabled},activeExt={_activeSubtitleWasExternal},parsed={_useParsedSubtitleRenderer},sidecar={(_playerSidecarSubtitleActive ? "yes" : "no")},{GetNativeSubtitleState()}";
            if (!string.IsNullOrWhiteSpace(details))
                state = $"{details} | {state}";

            if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureStreamDebugEvent(stage, $"stream={DescribeSubtitleStream(stream)} | {state}");
        }

        private void SyncSubtitleSelectionFromCurrentState()
        {
            if (_subtitleListContainer == null || _subtitleListContainer.ChildCount == 0)
            {
                _subtitleIndex = 0;
                return;
            }

            string selectedRowName = (!_subtitleEnabled || !_initialSubtitleIndex.HasValue)
                ? "OFF_INDEX"
                : _initialSubtitleIndex.Value.ToString(CultureInfo.InvariantCulture);

            int selectedRow = -1;
            int count = (int)_subtitleListContainer.ChildCount;
            for (int i = 0; i < count; i++)
            {
                var row = _subtitleListContainer.GetChildAt((uint)i);
                if (string.Equals(row?.Name, selectedRowName, StringComparison.Ordinal))
                {
                    selectedRow = i;
                    break;
                }
            }

            if (selectedRow < 0)
            {
                if (string.Equals(selectedRowName, "OFF_INDEX", StringComparison.Ordinal))
                {
                    selectedRow = 0;
                }
                else
                {
                    selectedRow = 0;
                    for (int i = 0; i < count; i++)
                    {
                        var row = _subtitleListContainer.GetChildAt((uint)i);
                        if (!string.Equals(row?.Name, "OFF_INDEX", StringComparison.Ordinal))
                        {
                            selectedRow = i;
                            break;
                        }
                    }
                }
            }

            _subtitleIndex = Math.Clamp(selectedRow, 0, count - 1);
        }

        private void UpdateSubtitleVisuals(bool setFocus = true)
        {
            if (_subtitleListContainer == null) return;
            int count = (int)_subtitleListContainer.ChildCount;
            for (int i = 0; i < count; i++)
            {
                var row = _subtitleListContainer.GetChildAt((uint)i);
                bool selected = (i == _subtitleIndex);
                UiFactory.SetButtonFocusState(row, focused: selected);
                AnimateFocusScale(row, selected ? new Vector3(1.05f, 1.05f, 1.0f) : Vector3.One);
            }
            // Keep subtitle selection fully manual; FocusManager + ScrollableBase can crash on some TVs.
        }

        private void MoveSubtitleSelection(int delta)
        {
            if (!_subtitleOverlayVisible || _subtitleListContainer == null) return;
            int count = (int)_subtitleListContainer.ChildCount;
            if (count == 0) return;
            int before = _subtitleIndex;
            _subtitleIndex = Math.Clamp(_subtitleIndex + delta, 0, count - 1);
            if (_subtitleIndex != before)
                if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureStreamDebugEvent("Subtitle.Move", $"from={before},to={_subtitleIndex},delta={delta}");
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

        private void RebuildSubtitleRows(List<MediaStream> subtitleStreams)
        {
            if (_subtitleListContainer == null)
                return;

            while (_subtitleListContainer.ChildCount > 0)
            {
                var child = _subtitleListContainer.GetChildAt(0);
                if (child == null)
                    break;

                _subtitleListContainer.Remove(child);
            }

            if (!_burnIn)
            {
                // OFF option is only meaningful when subtitles are sidecar/native, not burn-in.
                _subtitleListContainer.Add(CreateSubtitleRow("OFF", "OFF_INDEX"));
            }

            if (subtitleStreams == null)
                return;

            foreach (var stream in subtitleStreams)
            {
                var lang = !string.IsNullOrEmpty(stream.Language) ? stream.Language.ToUpper() : "UNKNOWN";
                var title = !string.IsNullOrEmpty(stream.DisplayTitle) ? stream.DisplayTitle : $"Sub {stream.Index}";
                var labelText = $"{lang} | {title}";
                if (stream.IsExternal) labelText += " (Ext)";

                _subtitleListContainer.Add(CreateSubtitleRow(labelText, stream.Index.ToString()));
            }
        }

        private static int? GetEmbeddedSubtitleOrdinal(MediaSourceInfo mediaSource, int jellyfinStreamIndex)
        {
            // Keep ordinal stable across playback profile renegotiation by counting only
            // runtime-switchable embedded tracks (unsupported codecs like PGS can disappear
            // or move between direct-play and transcode payloads).
            var embedded = GetNativeSwitchableEmbeddedSubtitleStreams(mediaSource);

            if (embedded == null || embedded.Count == 0)
                return null;

            int ordinal = embedded.FindIndex(s => s.Index == jellyfinStreamIndex);
            return ordinal >= 0 ? ordinal : null;
        }

        private static List<MediaStream> GetNativeSwitchableEmbeddedSubtitleStreams(MediaSourceInfo mediaSource)
        {
            return mediaSource?.MediaStreams?
                .Where(s =>
                    string.Equals(s.Type, "Subtitle", StringComparison.OrdinalIgnoreCase) &&
                    !s.IsExternal &&
                    IsSubtitleCodecRuntimeSwitchSupported(s.Codec))
                .OrderBy(s => s.Index)
                .ToList() ?? new List<MediaStream>();
        }

        private static bool HasUnsupportedEmbeddedSubtitleStreams(MediaSourceInfo mediaSource)
        {
            var embeddedSubtitles = mediaSource?.MediaStreams?
                .Where(s =>
                    string.Equals(s.Type, "Subtitle", StringComparison.OrdinalIgnoreCase) &&
                    !s.IsExternal)
                .ToList();

            if (embeddedSubtitles == null || embeddedSubtitles.Count == 0)
                return false;

            return embeddedSubtitles.Any(s => !IsSubtitleCodecRuntimeSwitchSupported(s.Codec));
        }

        private static string NormalizeSubtitleMatchToken(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }

        private bool TryResolveRequestedSubtitleByMetadata(List<MediaStream> subtitles, out MediaStream resolvedStream)
        {
            resolvedStream = null;
            if (subtitles == null || subtitles.Count == 0)
                return false;

            string requestedCodec = NormalizeSubtitleMatchToken(_requestedSubtitleCodec);
            string requestedLanguage = NormalizeSubtitleMatchToken(_requestedSubtitleLanguage);
            string requestedTitle = NormalizeSubtitleMatchToken(_requestedSubtitleDisplayTitle);
            bool hasMetadataHint =
                !string.IsNullOrWhiteSpace(requestedCodec) ||
                !string.IsNullOrWhiteSpace(requestedLanguage) ||
                !string.IsNullOrWhiteSpace(requestedTitle) ||
                _requestedSubtitleWasExternal.HasValue;
            if (!hasMetadataHint)
                return false;

            int bestScore = int.MinValue;
            MediaStream bestStream = null;
            foreach (var stream in subtitles)
            {
                if (stream == null)
                    continue;

                int score = 0;
                if (_requestedSubtitleWasExternal.HasValue && stream.IsExternal == _requestedSubtitleWasExternal.Value)
                    score += 10;
                if (!string.IsNullOrWhiteSpace(requestedCodec) &&
                    string.Equals(NormalizeSubtitleMatchToken(stream.Codec), requestedCodec, StringComparison.Ordinal))
                    score += 8;
                if (!string.IsNullOrWhiteSpace(requestedLanguage) &&
                    string.Equals(NormalizeSubtitleMatchToken(stream.Language), requestedLanguage, StringComparison.Ordinal))
                    score += 6;
                if (!string.IsNullOrWhiteSpace(requestedTitle) &&
                    string.Equals(NormalizeSubtitleMatchToken(stream.DisplayTitle), requestedTitle, StringComparison.Ordinal))
                    score += 12;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestStream = stream;
                }
            }

            if (bestStream == null || bestScore <= 0)
                return false;

            resolvedStream = bestStream;
            return true;
        }

        private void AlignRequestedSubtitleIndexForCurrentMediaSource(MediaSourceInfo mediaSource)
        {
            if (!_initialSubtitleIndex.HasValue || _initialSubtitleIndex.Value < 0 || mediaSource == null)
                return;

            var subtitles = mediaSource.MediaStreams?
                .Where(s => s.Type == "Subtitle")
                .OrderBy(s => s.Index)
                .ToList();
            if (subtitles == null || subtitles.Count == 0)
                return;

            bool indexExists = subtitles.Any(s => s.Index == _initialSubtitleIndex.Value);
            if (indexExists)
                return;

            int previousIndex = _initialSubtitleIndex.Value;
            MediaStream resolvedStream = null;
            string remapReason = null;

            if (_requestedEmbeddedSubtitleOrdinal.HasValue)
            {
                var embedded = GetNativeSwitchableEmbeddedSubtitleStreams(mediaSource);
                int requestedEmbeddedOrdinal = _requestedEmbeddedSubtitleOrdinal.Value;
                if (requestedEmbeddedOrdinal >= 0 && requestedEmbeddedOrdinal < embedded.Count)
                {
                    resolvedStream = embedded[requestedEmbeddedOrdinal];
                    remapReason = $"switchableEmbeddedOrd={requestedEmbeddedOrdinal}";
                }
            }
            if (resolvedStream == null && _requestedSubtitleOrdinalAll.HasValue)
            {
                int requestedAllOrdinal = _requestedSubtitleOrdinalAll.Value;
                if (requestedAllOrdinal >= 0 && requestedAllOrdinal < subtitles.Count)
                {
                    resolvedStream = subtitles[requestedAllOrdinal];
                    remapReason = $"allOrd={requestedAllOrdinal}";
                }
            }
            if (resolvedStream == null && TryResolveRequestedSubtitleByMetadata(subtitles, out var metadataMatched))
            {
                resolvedStream = metadataMatched;
                remapReason = "metadata";
            }

            if (resolvedStream == null)
                return;

            _initialSubtitleIndex = resolvedStream.Index;
            _requestedSubtitleCodec = resolvedStream.Codec;
            _requestedSubtitleLanguage = resolvedStream.Language;
            _requestedSubtitleDisplayTitle = resolvedStream.DisplayTitle;
            _requestedSubtitleWasExternal = resolvedStream.IsExternal;
            _requestedEmbeddedSubtitleOrdinal = resolvedStream.IsExternal
                ? null
                : GetEmbeddedSubtitleOrdinal(mediaSource, resolvedStream.Index);
            _requestedSubtitleOrdinalAll = subtitles.FindIndex(s => s.Index == resolvedStream.Index);
            if (_requestedSubtitleOrdinalAll.HasValue && _requestedSubtitleOrdinalAll.Value < 0)
                _requestedSubtitleOrdinalAll = null;
            _initialSubtitleCodecHint = resolvedStream.Codec;

            if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureSubtitleDebugEvent(
                "Subtitle.IndexRemap",
                resolvedStream,
                $"from={previousIndex},to={resolvedStream.Index},reason={remapReason ?? "-"},subCount={subtitles.Count}");
        }

        private bool HasUnavailableRequestedSubtitleForCurrentMediaSource(MediaSourceInfo mediaSource)
        {
            if (!_initialSubtitleIndex.HasValue || _initialSubtitleIndex.Value < 0 || mediaSource == null)
                return false;

            var subtitles = mediaSource.MediaStreams?
                .Where(s => string.Equals(s.Type, "Subtitle", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (subtitles == null || subtitles.Count == 0)
                return true;

            return !subtitles.Any(s => s.Index == _initialSubtitleIndex.Value);
        }

        private void ClearRequestedSubtitleSelectionState()
        {
            _subtitleEnabled = false;
            _initialSubtitleIndex = null;
            _requestedEmbeddedSubtitleOrdinal = null;
            _requestedSubtitleOrdinalAll = null;
            _requestedSubtitleLanguage = null;
            _requestedSubtitleDisplayTitle = null;
            _requestedSubtitleCodec = null;
            _initialSubtitleCodecHint = null;
            _requestedSubtitleWasExternal = null;
            _externalSubtitleIndex = null;
            _externalSubtitleMediaSourceId = null;
            _externalSubtitleCodec = null;
            _externalSubtitleLanguage = null;
            _activeSubtitleWasExternal = false;
            _playerSidecarSubtitleActive = false;
            _forceNativeEmbeddedSelectionOnRestart = false;
        }

        private void CaptureSubtitleTopology(string stage, MediaSourceInfo mediaSource)
        {
            if (!DebugSwitches.EnablePlaybackDebugOverlay)
                return;

            var subtitleStreams = mediaSource?.MediaStreams?
                .Where(s => s.Type == "Subtitle")
                .OrderBy(s => s.Index)
                .ToList() ?? new List<MediaStream>();

            string jellyfinList = subtitleStreams.Count == 0
                ? "-"
                : string.Join(
                    ",",
                    subtitleStreams.Select(s =>
                    {
                        string ext = s.IsExternal ? "ext" : "emb";
                        string lang = string.IsNullOrWhiteSpace(s.Language) ? "-" : s.Language.ToUpperInvariant();
                        return $"{s.Index}:{ext}:{lang}";
                    }));

            int nativeCount = -1;
            int nativeSelected = -2;
            var nativeLangs = new List<string>();
            try
            {
                if (_player != null)
                {
                    nativeCount = _player.SubtitleTrackInfo.GetCount();
                    nativeSelected = _player.SubtitleTrackInfo.Selected;
                    for (int i = 0; i < nativeCount; i++)
                    {
                        string nativeLanguage = "-";
                        if (TryGetNativeSubtitleLanguageKey(i, out var languageKey))
                            nativeLanguage = languageKey.ToUpperInvariant();
                        nativeLangs.Add($"{i}:{nativeLanguage}");
                    }
                }
            }
            catch
            {
            }

            string nativeList = nativeLangs.Count == 0 ? "-" : string.Join(",", nativeLangs);
            string reqIndex = _initialSubtitleIndex.HasValue ? _initialSubtitleIndex.Value.ToString(CultureInfo.InvariantCulture) : "OFF";
            string reqOrd = _requestedEmbeddedSubtitleOrdinal.HasValue ? _requestedEmbeddedSubtitleOrdinal.Value.ToString(CultureInfo.InvariantCulture) : "-";
            if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureStreamDebugEvent(
                stage,
                $"jf=[{jellyfinList}],reqIdx={reqIndex},reqEmbOrd={reqOrd},native={nativeSelected}/{nativeCount},langs=[{nativeList}]");
        }

        private void SelectSubtitle()
        {
            FireAndForget(SelectSubtitleAsync());
        }

        private async Task SelectSubtitleAsync()
        {
            if (_subtitleListContainer == null) return;
            if (_subtitleIndex < 0 || _subtitleIndex >= _subtitleListContainer.ChildCount) return;
            var selectedRow = _subtitleListContainer.GetChildAt((uint)_subtitleIndex);
            string selectedName = selectedRow.Name;
            if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureStreamDebugEvent("Subtitle.UISelect", $"row={_subtitleIndex},name={selectedName}");
            
            HideSubtitleOverlay();

            if (selectedName == "OFF_INDEX")
            {
                var activeStreamBeforeOff = (_initialSubtitleIndex.HasValue && _currentMediaSource != null)
                    ? _currentMediaSource.MediaStreams?.FirstOrDefault(s => s.Index == _initialSubtitleIndex.Value)
                    : null;
                bool requiresRestartForUnsupportedSubtitleOff =
                    !_burnIn &&
                    string.Equals(_playMethod, "Transcode", StringComparison.OrdinalIgnoreCase) &&
                    RequiresServerTranscodeSubtitleSwitch(activeStreamBeforeOff);

                _subtitleEnabled = false;
                _initialSubtitleIndex = null;
                _requestedEmbeddedSubtitleOrdinal = null;
                _requestedSubtitleOrdinalAll = null;
                _requestedSubtitleLanguage = null;
                _requestedSubtitleDisplayTitle = null;
                _requestedSubtitleCodec = null;
                _requestedSubtitleWasExternal = null;
                _forceNativeEmbeddedSelectionOnRestart = false;
                _playerSidecarSubtitleActive = false;
                _externalSubtitlePath = null;
                _activeSubtitleWasExternal = false;
                _useParsedSubtitleRenderer = false;
                ClearParsedSubtitleCues();
                StopSubtitleRenderTimer();
                _subtitleHideTimer?.Stop();
                try { _player?.ClearSubtitle(); } catch { }
                TryDisableNativeSubtitleTrack();
                SyncSubtitleSelectionFromCurrentState();
                if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureSubtitleDebugEvent("Subtitle.Off");
                RunOnUiThread(() =>
                {
                    _subtitleText?.Hide();
                });

                if (_burnIn || requiresRestartForUnsupportedSubtitleOff)
                {
                    if (DebugSwitches.EnablePlaybackDebugOverlay && requiresRestartForUnsupportedSubtitleOff)
                        CaptureSubtitleDebugEvent("Subtitle.BurnInOffRestart", activeStreamBeforeOff, "reason=unsupportedByProfile");
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

            var orderedSubtitleStreams = _currentMediaSource?.MediaStreams?
                .Where(s => string.Equals(s.Type, "Subtitle", StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.Index)
                .ToList();
            _requestedEmbeddedSubtitleOrdinal = subStream.IsExternal
                ? null
                : GetEmbeddedSubtitleOrdinal(_currentMediaSource, jellyfinStreamIndex);
            _requestedSubtitleOrdinalAll = orderedSubtitleStreams?.FindIndex(s => s.Index == jellyfinStreamIndex);
            if (_requestedSubtitleOrdinalAll.HasValue && _requestedSubtitleOrdinalAll.Value < 0)
                _requestedSubtitleOrdinalAll = null;
            _requestedSubtitleLanguage = subStream.Language;
            _requestedSubtitleDisplayTitle = subStream.DisplayTitle;
            _requestedSubtitleCodec = subStream.Codec;
            _initialSubtitleCodecHint = subStream.Codec;
            _requestedSubtitleWasExternal = subStream.IsExternal;
            if (subStream.IsExternal)
                _forceNativeEmbeddedSelectionOnRestart = false;

            bool switchingFromExternalRenderer = _activeSubtitleWasExternal || _playerSidecarSubtitleActive;
            bool isDirectPlay = string.Equals(_playMethod, "DirectPlay", StringComparison.OrdinalIgnoreCase);
            int? previousSubtitleIndex = _initialSubtitleIndex;
            var previousSubStream = (previousSubtitleIndex.HasValue && _currentMediaSource != null)
                ? _currentMediaSource.MediaStreams?.FirstOrDefault(s => s.Index == previousSubtitleIndex.Value)
                : null;
            bool subtitleSelectionUnchanged =
                previousSubtitleIndex.HasValue &&
                previousSubtitleIndex.Value == jellyfinStreamIndex &&
                _subtitleEnabled;
            if (subtitleSelectionUnchanged && isDirectPlay && !subStream.IsExternal)
            {
                int selectedNativeSlot = -2;
                try { selectedNativeSlot = _player?.SubtitleTrackInfo.Selected ?? -2; } catch { }
                int expectedNativeSlot = GetExpectedNativeSubtitleSlot() ?? -1;
                bool nativeSelectionMatches = expectedNativeSlot >= 0 && selectedNativeSlot == expectedNativeSlot;
                subtitleSelectionUnchanged = nativeSelectionMatches;
            }
            if (subtitleSelectionUnchanged)
            {
                if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureSubtitleDebugEvent("Subtitle.SelectSkip", subStream, "reason=unchanged");
                return;
            }
            _initialSubtitleIndex = jellyfinStreamIndex;
            SyncSubtitleSelectionFromCurrentState();
            if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureSubtitleDebugEvent("Subtitle.Select", subStream, $"prev={previousSubtitleIndex?.ToString(CultureInfo.InvariantCulture) ?? "OFF"},switchFromExt={switchingFromExternalRenderer}");
            if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureSubtitleTopology("Subtitle.SelectTopology", _currentMediaSource);
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
                bool requiresRestartForUnsupportedSubtitleOnDirectPlay =
                    string.Equals(_playMethod, "DirectPlay", StringComparison.OrdinalIgnoreCase) &&
                    RequiresServerTranscodeSubtitleSwitch(subStream);
                if (requiresRestartForUnsupportedSubtitleOnDirectPlay)
                {
                    if (DebugSwitches.EnablePlaybackDebugOverlay)
                        CaptureSubtitleDebugEvent("Subtitle.DirectPlayToBurnInRestart", subStream, $"reason=unsupportedByProfile,newCodec={subStream?.Codec ?? "-"}");
                    _forceNativeEmbeddedSelectionOnRestart = false;
                    long currentPos = GetPlayPositionMs();
                    _suppressStopReportOnce = true;
                    StopPlayback();
                    _startPositionMs = (int)currentPos;
                    StartPlayback();
                    return;
                }

                bool requiresRestartForUnsupportedSubtitleSwitch =
                    string.Equals(_playMethod, "Transcode", StringComparison.OrdinalIgnoreCase) &&
                    (RequiresServerTranscodeSubtitleSwitch(subStream) || RequiresServerTranscodeSubtitleSwitch(previousSubStream));

                if (requiresRestartForUnsupportedSubtitleSwitch)
                {
                    if (DebugSwitches.EnablePlaybackDebugOverlay)
                        CaptureSubtitleDebugEvent("Subtitle.BurnInSwitchRestart", subStream, $"reason=unsupportedByProfile,prevCodec={previousSubStream?.Codec ?? "-"},newCodec={subStream?.Codec ?? "-"}");
                    long currentPos = GetPlayPositionMs();
                    _suppressStopReportOnce = true;
                    StopPlayback();
                    _startPositionMs = (int)currentPos;
                    StartPlayback();
                    return;
                }

                if (!subStream.IsExternal && isDirectPlay)
                {
                    if (switchingFromExternalRenderer)
                    {
                        // Best effort reset: clear any sidecar/parsed renderer state before native selection.
                        if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureSubtitleDebugEvent("Subtitle.ExternalToEmbeddedReset", subStream, "action=nativeSwitch");
                        _externalSubtitlePath = null;
                        _playerSidecarSubtitleActive = false;
                        _useParsedSubtitleRenderer = false;
                        ClearParsedSubtitleCues();
                        StopSubtitleRenderTimer();
                        _subtitleHideTimer?.Stop();
                        try { _player?.ClearSubtitle(); } catch { }
                        RunOnUiThread(() =>
                        {
                            _subtitleText?.Hide();
                        });
                    }

                    if (!TrySelectNativeEmbeddedSubtitle(jellyfinStreamIndex))
                    {
                        await Task.Delay(120);
                        if (!TrySelectNativeEmbeddedSubtitle(jellyfinStreamIndex))
                        {
                            if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureSubtitleDebugEvent("Subtitle.NativeSwitchFailRestart", subStream);
                            _forceNativeEmbeddedSelectionOnRestart = true;
                            long currentPos = GetPlayPositionMs();
                            _suppressStopReportOnce = true;
                            StopPlayback();
                            _startPositionMs = (int)currentPos;
                            StartPlayback();
                            return;
                        }
                    }
                    ApplySubtitleOffset();
                    return;
                }

                // Non-directplay path always relies on Jellyfin delivery URL + parsed renderer.
                if (await DownloadAndSetSubtitle(_currentMediaSource.Id, subStream))
                {
                    ApplySubtitleOffset();
                    if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureSubtitleDebugEvent("Subtitle.ParserSwitchSuccess", subStream);
                    return;
                }

                if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureSubtitleDebugEvent("Subtitle.ParserSwitchFail", subStream);
            }
        }

        private void CreateAudioOverlay()
        {
            _audioOverlayBaseX = Window.Default.Size.Width - 500;
            _audioOverlay = new View
            {
                WidthSpecification = UiTheme.PlayerOverlayWidth,
                HeightSpecification = UiTheme.PlayerOverlayHeight,
                BackgroundColor = MonochromeAuthFactory.PanelFallbackColor,
                PositionX = _audioOverlayBaseX + OverlaySlideDistance,
                PositionY = Window.Default.Size.Height - 780,
                CornerRadius = MonochromeAuthFactory.PanelCornerRadius,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BorderlineWidth = MonochromeAuthFactory.PanelBorderWidth,
                BorderlineColor = MonochromeAuthFactory.PanelFallbackBorder,
                ClippingMode = ClippingModeType.ClipChildren,
                Opacity = 0.0f
            };
            _audioOverlay.Hide();
            Add(_audioOverlay);
        }

        private void ShowAudioOverlay()
        {
            if (_player == null) return;
            if (_audioOverlay == null) CreateAudioOverlay();
            HideSubtitleOverlay();
            HidePlaybackInfoOverlay();
            ExitSubtitleOffsetAdjustMode();
            var wasVisible = _audioOverlayVisible;

            var audioStreams = GetOrderedAudioStreams();

            if (audioStreams == null || audioStreams.Count == 0) return;
            
            if (_audioOverlay.ChildCount == 0)
            {
                _audioOverlay.Add(new TextLabel("Audio Tracks") { WidthResizePolicy = ResizePolicyType.FillToParent, HeightSpecification = UiTheme.PlayerOverlayHeaderHeight, PointSize = UiTheme.PlayerOverlayHeader, TextColor = Color.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
                _audioScrollView = new ScrollableBase { WidthResizePolicy = ResizePolicyType.FillToParent, HeightResizePolicy = ResizePolicyType.FillToParent, PositionY = UiTheme.PlayerOverlayHeaderHeight, HeightSpecification = UiTheme.PlayerAudioScrollHeight, ScrollingDirection = ScrollableBase.Direction.Vertical };
                _audioListContainer = new View { WidthResizePolicy = ResizePolicyType.FillToParent, HeightResizePolicy = ResizePolicyType.FitToChildren, Layout = new LinearLayout { LinearOrientation = LinearLayout.Orientation.Vertical, CellPadding = new Size2D(0, 5) } };
                _audioScrollView.Add(_audioListContainer);
                _audioOverlay.Add(_audioScrollView);

                foreach (var stream in audioStreams)
                {
                    var lang = !string.IsNullOrEmpty(stream.Language) ? stream.Language.ToUpper() : "UNKNOWN";
                    var codec = !string.IsNullOrEmpty(stream.Codec) ? stream.Codec.ToUpper() : "AUDIO";
                    var displayText = $"{lang} | {codec}";

                    var row = new View
                    {
                        WidthResizePolicy = ResizePolicyType.FillToParent,
                        HeightSpecification = UiTheme.PlayerOverlayListRowHeight,
                        BackgroundColor = Color.Black,
                        CornerRadius = UiTheme.PlayerOverlayListRowHeight / 2.0f,
                        CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                        BorderlineWidth = 2.0f,
                        BorderlineColor = Color.White,
                        Margin = new Extents(UiTheme.PlayerOverlayItemMarginHorizontal, UiTheme.PlayerOverlayItemMarginHorizontal, UiTheme.PlayerOverlayItemMarginVertical, UiTheme.PlayerOverlayItemMarginVertical),
                        Focusable = false,
                        ClippingMode = ClippingModeType.ClipChildren
                    };
                    row.Name = stream.Index.ToString();
                    var label = new TextLabel(displayText) { WidthResizePolicy = ResizePolicyType.FillToParent, HeightResizePolicy = ResizePolicyType.FillToParent, PointSize = UiTheme.PlayerAudioItem, TextColor = Color.White, HorizontalAlignment = HorizontalAlignment.Begin, VerticalAlignment = VerticalAlignment.Center, Padding = new Extents(UiTheme.PlayerOverlayItemPaddingLeft, (ushort)0, (ushort)0, (ushort)0) };
                    row.Add(label);
                    UiFactory.SetButtonFocusState(row, focused: false);
                    _audioListContainer.Add(row);
                }
            }
            SyncAudioSelectionFromPlaybackState(audioStreams);
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
                UiFactory.SetButtonFocusState(row, focused: selected);
                AnimateFocusScale(row, selected ? new Vector3(1.05f, 1.05f, 1.0f) : Vector3.One);
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
                    _subtitleHideDeadlineUtc = DateTime.MinValue;
                    _subtitleText?.Hide();
                    return;
                }

                _subtitleText.Text = normalizedText;
                _subtitleText.Show();

                uint hideDurationMs = (uint)Math.Clamp((long)e.Duration, 200L, uint.MaxValue);
                RestartSubtitleHideTimer(hideDurationMs);
            });
        }

        private void RestartSubtitleHideTimer(uint hideDurationMs)
        {
            _subtitleHideDeadlineUtc = DateTime.UtcNow.AddMilliseconds(hideDurationMs);
            _subtitleHideTimer ??= new Timer(120);
            _subtitleHideTimer.Stop();
            _subtitleHideTimer.Tick -= OnSubtitleHideTimerTick;
            _subtitleHideTimer.Tick += OnSubtitleHideTimerTick;
            _subtitleHideTimer.Start();
        }

        private bool OnSubtitleHideTimerTick(object sender, Timer.TickEventArgs e)
        {
            if (_subtitleText == null)
                return false;

            if (DateTime.UtcNow < _subtitleHideDeadlineUtc)
                return true;

            _subtitleText.Hide();
            return false;
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
            
            bool isDirectPlayableByProfile = IsAudioCodecDirectPlayableByProfile(codec);
            bool canTryNativeDirectPlaySwitch =
                string.Equals(_playMethod, "DirectPlay", StringComparison.OrdinalIgnoreCase) &&
                isDirectPlayableByProfile;

            _audioSelectionUserOverride = true;
            if (canTryNativeDirectPlaySwitch)
            {
                if (TrySelectNativeAudioTrack(jellyfinStreamIndex))
                    return;
            }

            long currentPos = GetPlayPositionMs();
            _overrideAudioIndex = jellyfinStreamIndex;
            _suppressStopReportOnce = true;
            StopPlayback();
            _startPositionMs = (int)currentPos;
            StartPlayback();
        }

        private void ShowOSD()
        {
            EnsureOsdCreated();
            if (_osd == null)
                return;

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

            bool seekbarRowFocused = _osdFocusRow == 0;
            _progressTrack.BackgroundColor = UiTheme.PlayerTrackIdle;
            if (_seekbarFocusVisualActive != seekbarRowFocused)
            {
                _seekbarFocusVisualActive = seekbarRowFocused;
                var lineScale = seekbarRowFocused
                    ? new Vector3(1.0f, 1.22f, 1f)
                    : Vector3.One;
                AnimateFocusScale(
                    _progressTrack,
                    lineScale
                );
                AnimateFocusScale(
                    _progressFill,
                    lineScale
                );
                AnimateFocusScale(
                    _previewFill,
                    lineScale
                );
                AnimateFocusScale(
                    _progressThumb,
                    seekbarRowFocused ? new Vector3(1.14f, 1.14f, 1f) : Vector3.One
                );
            }

            bool buttonRowFocused = _osdFocusRow == 1;
            SetButtonVisual(_audioButton, buttonRowFocused && _buttonFocusIndex == AudioButtonIndex);
            SetButtonVisual(_subtitleButton, buttonRowFocused && _buttonFocusIndex == SubtitleButtonIndex);
            SetButtonVisual(_nextButton, _movie.ItemType == "Episode" && buttonRowFocused && _buttonFocusIndex == NextButtonIndex);
            int aspectButtonIndex = GetAspectButtonIndex();
            SetButtonVisual(_aspectButton, buttonRowFocused && aspectButtonIndex >= 0 && _buttonFocusIndex == aspectButtonIndex);

            if (_subtitleOffsetCenterMarker != null)
            {
                _subtitleOffsetCenterMarker.BackgroundColor = UiTheme.PlayerSubtitleOffsetCenter;
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

        private void EnsureOsdCreated()
        {
            if (_osd != null)
                return;

            CreateOSD();
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
            _reportedPlayMethod = "DirectPlay";
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
            _playbackInfoOverlayVisible = false;
            _subtitleOffsetAdjustMode = false;
            UiAnimator.StopAndDispose(ref _osdAnimation);
            UiAnimator.StopAndDispose(ref _topOsdAnimation);
            UiAnimator.StopAndDispose(ref _subtitleOverlayAnimation);
            UiAnimator.StopAndDispose(ref _audioOverlayAnimation);
            UiAnimator.StopAndDispose(ref _playbackInfoOverlayAnimation);
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
            _playbackInfoOverlay?.Hide();
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
            _externalSubtitlePath = null;
            _activeSubtitleWasExternal = false;
            _forceNativeEmbeddedSelectionOnRestart = false;
            _playerSidecarSubtitleActive = false;
            if (DebugSwitches.EnablePlaybackDebugOverlay) CaptureSubtitleDebugEvent("StopPlayback");
            
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
                ClearParsedSubtitleCues();
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
                    if (_playbackInfoOverlayVisible)
                    {
                    }
                    else if (_audioOverlayVisible) SelectAudioTrack();
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
                    if (_playbackInfoOverlayVisible)
                    {
                    }
                    else if (_audioOverlayVisible)
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
                    if (_playbackInfoOverlayVisible)
                    {
                    }
                    else if (_audioOverlayVisible)
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
                    if (!_osdVisible && TryScrollStreamDebugOverlay(-1))
                        break;
                    if (_playbackInfoOverlayVisible)
                    {
                        TryScrollPlaybackInfoOverlay(-1);
                    }
                    else if (_audioOverlayVisible)
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
                    if (!_osdVisible && TryScrollStreamDebugOverlay(1))
                        break;
                    if (_playbackInfoOverlayVisible)
                    {
                        TryScrollPlaybackInfoOverlay(1);
                    }
                    else if (_audioOverlayVisible)
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
                    if (_playbackInfoOverlayVisible)
                    {
                        HidePlaybackInfoOverlay();
                    }
                    else if (_subtitleOverlayVisible) 
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
                case AppKey.Red:
                    TogglePlaybackInfoOverlay();
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
            if (_buttonFocusIndex == AudioButtonIndex)
            {
                ShowAudioOverlay();
                return;
            }

            if (_buttonFocusIndex == SubtitleButtonIndex)
            {
                ShowSubtitleOverlay();
                return;
            }

            if (_buttonFocusIndex == GetAspectButtonIndex())
            {
                ToggleAspectMode();
                return;
            }

            if (_movie.ItemType == "Episode" && _buttonFocusIndex == NextButtonIndex)
            {
                PlayNextEpisode();
            }
        }
        
        private void PlayNextEpisode()
        {
            FireAndForget(PlayNextEpisodeAsync());
        }

        private async Task PlayNextEpisodeAsync()
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
                var orderedEpisodes = (await AppState.Jellyfin.GetEpisodesAsync(_movie.SeriesId, lightweight: true) ?? new List<JellyfinMovie>())
                    .Where(e => e != null && e.ItemType == "Episode")
                    .OrderBy(e => e.ParentIndexNumber)
                    .ThenBy(e => e.IndexNumber)
                    .ThenBy(e => e.Name)
                    .ToList();

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
            if (_progressFill != null)
                _progressFill.WidthSpecification = 0;
            if (_currentTimeLabel != null)
                _currentTimeLabel.Text = "00:00";
            if (_durationLabel != null)
                _durationLabel.Text = "-00:00";
            if (_clockLabel != null) _clockLabel.Text = FormatClockTime(DateTime.Now);
            if (_endsAtLabel != null) _endsAtLabel.Text = string.Empty;
            _overrideAudioIndex = null;
            _audioSelectionUserOverride = false;
            _preferredMediaSourceId = null;
            _currentMediaSource = null;
            _useParsedSubtitleRenderer = false;
            ClearParsedSubtitleCues();
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
