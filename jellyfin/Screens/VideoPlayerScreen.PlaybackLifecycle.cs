using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JellyfinTizen.Core;
using JellyfinTizen.Models;
using Tizen.Multimedia;

namespace JellyfinTizen.Screens
{
    public partial class VideoPlayerScreen
    {
        private const int NoPendingAudioSelection = -1;
        private const int HlsManifestProbeMaxBytes = 64 * 1024;

        private readonly SemaphoreSlim _playbackTransitionGate = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _playbackSessionCancellation;
        private CancellationTokenSource _subtitleOperationCancellation;
        private CancellationTokenSource _seekSafetyCancellation;
        private PlayerEventBindings _playerEventBindings;
        private int _activeNativePlayerGeneration = -1;
        private int _playbackTransitionCount;
        private int _restartInProgress;
        private int _startRequestInProgress;
        private int _audioSwitchRequestInProgress;
        private int _nativeSeekInProgress;
        private int _screenHidden;
        private int _latestRequestedAudioIndex = NoPendingAudioSelection;
        private bool _playbackShouldBePlaying = true;
        private int _bufferingActive;
        private int _lastBufferingLogPercent = -1;
        private long _bufferingStartedTimestamp;
        private long _playbackStartupTimestamp;
        private long _lastNativePlayerDisposeTimestamp;
        private bool _firstPlaybackPositionLogged;

        private sealed class PlayerEventBindings
        {
            public EventHandler<EventArgs> PlaybackCompleted;
            public EventHandler<PlayerErrorOccurredEventArgs> ErrorOccurred;
            public EventHandler<BufferingProgressChangedEventArgs> BufferingProgressChanged;
            public EventHandler<SubtitleUpdatedEventArgs> SubtitleUpdated;
        }

        private bool IsPlaybackTransitionRunning =>
            Volatile.Read(ref _playbackTransitionCount) > 0 ||
            Volatile.Read(ref _restartInProgress) != 0;

        private bool IsScreenHidden => Volatile.Read(ref _screenHidden) != 0;

        private CancellationToken CurrentPlaybackCancellationToken
        {
            get
            {
                var cancellation =
                    Volatile.Read(ref _playbackSessionCancellation);
                if (cancellation == null)
                    return new CancellationToken(canceled: true);

                try
                {
                    return cancellation.Token;
                }
                catch (ObjectDisposedException)
                {
                    return new CancellationToken(canceled: true);
                }
            }
        }

        private int BeginPlaybackGeneration()
        {
            CancelPlaybackOperationTokens();

            int generation = Interlocked.Increment(ref _playbackToken);
            var sessionCancellation = new CancellationTokenSource();
            var subtitleCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(sessionCancellation.Token);

            Interlocked.Exchange(
                    ref _playbackSessionCancellation,
                    sessionCancellation)
                ?.Dispose();
            Interlocked.Exchange(
                    ref _subtitleOperationCancellation,
                    subtitleCancellation)
                ?.Dispose();

            _activeNativePlayerGeneration = -1;
            _playbackStartupTimestamp = Stopwatch.GetTimestamp();
            _firstPlaybackPositionLogged = false;
            Interlocked.Exchange(ref _bufferingActive, 0);
            _lastBufferingLogPercent = -1;
            _bufferingStartedTimestamp = 0;
            return generation;
        }

        private void InvalidatePlaybackGeneration()
        {
            Interlocked.Increment(ref _playbackToken);
            CancelPlaybackOperationTokens();
        }

        private void CancelPlaybackOperationTokens()
        {
            CancelAndDisposeCancellation(
                Interlocked.Exchange(ref _seekSafetyCancellation, null));
            CancelAndDisposeCancellation(
                Interlocked.Exchange(ref _subtitleOperationCancellation, null));
            CancelAndDisposeCancellation(
                Interlocked.Exchange(ref _playbackSessionCancellation, null));
        }

        private static void CancelAndDisposeCancellation(CancellationTokenSource cancellation)
        {
            if (cancellation == null)
                return;

            try
            {
                cancellation.Cancel();
            }
            catch
            {
            }
            finally
            {
                cancellation.Dispose();
            }
        }

        private CancellationToken BeginSubtitleOperation()
        {
            var sessionCancellation =
                Volatile.Read(ref _playbackSessionCancellation);
            if (sessionCancellation == null)
                return new CancellationToken(canceled: true);

            CancellationTokenSource operationCancellation;
            try
            {
                operationCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        sessionCancellation.Token);
            }
            catch (ObjectDisposedException)
            {
                return new CancellationToken(canceled: true);
            }

            CancelAndDisposeCancellation(
                Interlocked.Exchange(
                    ref _subtitleOperationCancellation,
                    operationCancellation));
            return operationCancellation.Token;
        }

        private bool IsCurrentPlaybackGeneration(int generation)
        {
            return !IsScreenHidden && generation == Volatile.Read(ref _playbackToken);
        }

        private bool IsCurrentPlayer(Player player, int generation)
        {
            return player != null &&
                   IsCurrentPlaybackGeneration(generation) &&
                   ReferenceEquals(player, Volatile.Read(ref _player)) &&
                   generation == Volatile.Read(ref _activeNativePlayerGeneration);
        }

        private async Task RunStartPlaybackTransitionAsync()
        {
            if (Interlocked.CompareExchange(ref _startRequestInProgress, 1, 0) != 0)
            {
                LogPlaybackDiagnostic("Start.Ignored", "reason=start-already-in-progress");
                return;
            }

            Interlocked.Increment(ref _playbackTransitionCount);
            await _playbackTransitionGate.WaitAsync();
            try
            {
                if (IsScreenHidden || IsStopRequested())
                    return;

                bool replacingExistingPlayer = _player != null;
                bool shouldBePlaying = CaptureDesiredPlaybackState();
                int resumePositionMs = replacingExistingPlayer
                    ? Math.Max(0, GetPlayPositionMs())
                    : Math.Max(0, _startPositionMs);

                if (replacingExistingPlayer)
                {
                    EndPlaybackWithoutStop(PlaybackStopReason.PlaybackRestart);
                    InvalidatePlaybackGeneration();
                    await StopPlaybackLocalAsync("start-replace");
                }

                int generation = BeginPlaybackGeneration();
                var cancellationToken = CurrentPlaybackCancellationToken;

                await WaitForNativeDrainAsync(generation, cancellationToken);
                if (replacingExistingPlayer)
                    _startPositionMs = resumePositionMs;

                await StartPlaybackCoreAsync(generation, cancellationToken, shouldBePlaying);
            }
            catch (OperationCanceledException) when (
                IsScreenHidden ||
                IsStopRequested())
            {
                LogPlaybackDiagnostic(
                    "Start.Cancelled",
                    $"reason={(IsScreenHidden ? "screen-hidden" : "stop-requested")}");
            }
            finally
            {
                _playbackTransitionGate.Release();
                Interlocked.Decrement(ref _playbackTransitionCount);
                Interlocked.Exchange(ref _startRequestInProgress, 0);
            }
        }

        private async Task RestartPlaybackAsync(
            string stage,
            PlaybackStopReason stopReason,
            Action applySelection,
            bool restoreCurrentPosition = true,
            Action configureNewSession = null,
            bool? shouldBePlayingOverride = null)
        {
            if (Interlocked.CompareExchange(ref _restartInProgress, 1, 0) != 0)
            {
                LogPlaybackDiagnostic(
                    $"{stage}.Ignored",
                    "reason=restart-already-in-progress");
                return;
            }

            Interlocked.Increment(ref _playbackTransitionCount);
            await _playbackTransitionGate.WaitAsync();
            try
            {
                if (IsScreenHidden || IsStopRequested())
                    return;

                await RestartPlaybackUnderGateAsync(
                    stage,
                    stopReason,
                    applySelection,
                    restoreCurrentPosition,
                    configureNewSession,
                    shouldBePlayingOverride);
            }
            catch (OperationCanceledException) when (
                IsScreenHidden ||
                IsStopRequested())
            {
                LogPlaybackDiagnostic(
                    $"{stage}.Cancelled",
                    $"reason={(IsScreenHidden ? "screen-hidden" : "stop-requested")}");
            }
            finally
            {
                _playbackTransitionGate.Release();
                Interlocked.Decrement(ref _playbackTransitionCount);
                Interlocked.Exchange(ref _restartInProgress, 0);
            }
        }

        private async Task RestartPlaybackUnderGateAsync(
            string stage,
            PlaybackStopReason stopReason,
            Action applySelection,
            bool restoreCurrentPosition,
            Action configureNewSession,
            bool? shouldBePlayingOverride)
        {
            int resumePositionMs = Math.Max(0, GetPlayPositionMs());
            bool shouldBePlaying =
                shouldBePlayingOverride ?? CaptureDesiredPlaybackState();

            EndPlaybackWithoutStop(stopReason);
            InvalidatePlaybackGeneration();
            await StopPlaybackLocalAsync(stage);

            int generation = BeginPlaybackGeneration();
            var cancellationToken = CurrentPlaybackCancellationToken;
            LogPlaybackDiagnostic(
                $"{stage}.Begin",
                $"generation={generation},positionMs={resumePositionMs},resume={shouldBePlaying}");

            await WaitForNativeDrainAsync(generation, cancellationToken);

            if (!IsCurrentPlaybackGeneration(generation))
                return;

            configureNewSession?.Invoke();
            applySelection?.Invoke();
            if (restoreCurrentPosition)
                _startPositionMs = resumePositionMs;

            await StartPlaybackCoreAsync(generation, cancellationToken, shouldBePlaying);
            if (IsCurrentPlayer(_player, generation))
            {
                LogPlaybackDiagnostic(
                    $"{stage}.Complete",
                    $"generation={generation},audio={_overrideAudioIndex?.ToString(CultureInfo.InvariantCulture) ?? "-"}");
            }
            else
            {
                LogPlaybackDiagnostic(
                    $"{stage}.Incomplete",
                    $"generation={generation},reason=no-current-player");
            }
        }

        private async Task WaitForNativeDrainAsync(
            int generation,
            CancellationToken cancellationToken)
        {
            long disposedAt = Interlocked.Read(
                ref _lastNativePlayerDisposeTimestamp);
            if (disposedAt <= 0)
                return;

            int elapsedMs = (int)Math.Min(
                int.MaxValue,
                GetElapsedMilliseconds(disposedAt));
            int remainingMs = Math.Max(
                0,
                AppState.PlayerNativeDrainDelayMs - elapsedMs);
            if (remainingMs > 0)
                await Task.Delay(remainingMs, cancellationToken);

            if (!IsCurrentPlaybackGeneration(generation))
                throw new OperationCanceledException(cancellationToken);

            LogPlaybackMemorySnapshot("after-native-drain", null, generation);
        }

        private bool CaptureDesiredPlaybackState()
        {
            var player = _player;
            if (player == null)
                return _playbackShouldBePlaying;

            try
            {
                if (player.State == PlayerState.Paused)
                    return false;
                if (player.State == PlayerState.Playing)
                    return true;
            }
            catch
            {
            }

            return _playbackShouldBePlaying;
        }

        private async Task SwitchAudioTrackAsync(int requestedStreamIndex)
        {
            Interlocked.Exchange(ref _latestRequestedAudioIndex, requestedStreamIndex);
            if (Interlocked.CompareExchange(ref _audioSwitchRequestInProgress, 1, 0) != 0)
            {
                LogPlaybackDiagnostic(
                    "AudioSwitch.Queued",
                    $"audio={requestedStreamIndex}");
                return;
            }

            int appliedStreamIndex = NoPendingAudioSelection;
            Interlocked.Increment(ref _playbackTransitionCount);
            await _playbackTransitionGate.WaitAsync();
            try
            {
                if (IsScreenHidden || IsStopRequested())
                    return;

                int selectedStreamIndex =
                    Volatile.Read(ref _latestRequestedAudioIndex);
                appliedStreamIndex = selectedStreamIndex;
                var selectedStream = _currentMediaSource?.MediaStreams?
                    .FirstOrDefault(s =>
                        string.Equals(s.Type, "Audio", StringComparison.OrdinalIgnoreCase) &&
                        s.Index == selectedStreamIndex);
                if (selectedStream == null)
                    return;

                _audioSelectionUserOverride = true;
                string codec = selectedStream.Codec?.ToLowerInvariant() ?? string.Empty;
                bool canTryNativeDirectPlaySwitch =
                    string.Equals(_playMethod, "DirectPlay", StringComparison.OrdinalIgnoreCase) &&
                    IsAudioCodecDirectPlayableByProfile(codec);

                if (canTryNativeDirectPlaySwitch &&
                    TrySelectNativeAudioTrack(selectedStreamIndex))
                {
                    LogPlaybackDiagnostic(
                        "AudioSwitch.Native",
                        $"audio={selectedStreamIndex},codec={codec}");
                    ReportProgressToServer(force: true);
                    return;
                }

                if (Interlocked.CompareExchange(ref _restartInProgress, 1, 0) != 0)
                    return;

                try
                {
                    await RestartPlaybackUnderGateAsync(
                        "AudioSwitch.Restart",
                        PlaybackStopReason.PlaybackRestart,
                        () =>
                        {
                            appliedStreamIndex = Volatile.Read(
                                ref _latestRequestedAudioIndex);
                            _overrideAudioIndex = appliedStreamIndex;
                        },
                        restoreCurrentPosition: true,
                        configureNewSession: null,
                        shouldBePlayingOverride: null);
                }
                finally
                {
                    Interlocked.Exchange(ref _restartInProgress, 0);
                }
            }
            catch (OperationCanceledException) when (
                IsScreenHidden ||
                IsStopRequested())
            {
                LogPlaybackDiagnostic(
                    "AudioSwitch.Cancelled",
                    $"reason={(IsScreenHidden ? "screen-hidden" : "stop-requested")}");
            }
            finally
            {
                _playbackTransitionGate.Release();
                Interlocked.Decrement(ref _playbackTransitionCount);
                Interlocked.Exchange(ref _audioSwitchRequestInProgress, 0);

                int latestStreamIndex =
                    Volatile.Read(ref _latestRequestedAudioIndex);
                if (latestStreamIndex != NoPendingAudioSelection &&
                    latestStreamIndex != appliedStreamIndex &&
                    !IsScreenHidden &&
                    !IsStopRequested())
                {
                    FireAndForget(
                        SwitchAudioTrackAsync(latestStreamIndex),
                        nameof(SwitchAudioTrackAsync));
                }
            }
        }

        private Player CreateNativePlayer(int generation)
        {
            if (!IsCurrentPlaybackGeneration(generation))
                throw new OperationCanceledException(CurrentPlaybackCancellationToken);
            if (_player != null)
                throw new InvalidOperationException(
                    "A native Player already exists for this screen.");

            var player = new Player();
            var bindings = new PlayerEventBindings();

            bindings.ErrorOccurred = (sender, args) =>
            {
                if (IsCurrentPlayer(player, generation))
                    OnPlayerErrorOccurred(sender, args);
            };
            bindings.BufferingProgressChanged = (sender, args) =>
            {
                if (IsCurrentPlayer(player, generation))
                    OnBufferingProgressChanged(sender, args);
            };
            bindings.PlaybackCompleted = (sender, args) =>
            {
                if (IsCurrentPlayer(player, generation))
                    OnPlaybackCompleted(sender, args);
            };
            bindings.SubtitleUpdated = (sender, args) =>
            {
                if (IsCurrentPlayer(player, generation))
                    OnSubtitleUpdated(sender, args);
            };

            try
            {
                _player = player;
                _playerEventBindings = bindings;
                Volatile.Write(ref _activeNativePlayerGeneration, generation);

                player.BufferingTime =
                    new PlayerBufferingTime(PlayerBufferInitialMs, PlayerBufferResumeMs);
                player.ErrorOccurred += bindings.ErrorOccurred;
                player.BufferingProgressChanged += bindings.BufferingProgressChanged;
                player.PlaybackCompleted += bindings.PlaybackCompleted;
                player.SubtitleUpdated += bindings.SubtitleUpdated;
                player.Display = new Display(Tizen.NUI.Window.Default);
                player.DisplaySettings.Mode = PlayerDisplayMode.LetterBox;
                player.DisplaySettings.IsVisible = true;

                LogPlaybackMemorySnapshot("after-player-create", player, generation);
                return player;
            }
            catch
            {
                if (ReferenceEquals(
                        Volatile.Read(ref _player),
                        player))
                {
                    // Creation and configuration run while the transition gate is
                    // held. Route the partially initialized instance through the
                    // same deterministic teardown as every prepared player so
                    // Stop, Unprepare and Dispose are all independently attempted.
                    StopPlaybackLocalCore("player-create-failure");
                }
                else
                {
                    // Defensive fallback for an instance detached by a terminal
                    // lifecycle request between native calls.
                    RunPlaybackCleanupStep(
                        () => player.PlaybackCompleted -= bindings.PlaybackCompleted,
                        "create-failure-unsubscribe-completed");
                    RunPlaybackCleanupStep(
                        () => player.ErrorOccurred -= bindings.ErrorOccurred,
                        "create-failure-unsubscribe-error");
                    RunPlaybackCleanupStep(
                        () => player.BufferingProgressChanged -= bindings.BufferingProgressChanged,
                        "create-failure-unsubscribe-buffering");
                    RunPlaybackCleanupStep(
                        () => player.SubtitleUpdated -= bindings.SubtitleUpdated,
                        "create-failure-unsubscribe-subtitle");
                    try
                    {
                        RunPlaybackCleanupStep(
                            () => player.DisplaySettings.IsVisible = false,
                            "create-failure-hide-display");
                        RunPlaybackCleanupStep(player.Stop, "create-failure-stop");
                    }
                    finally
                    {
                        try
                        {
                            RunPlaybackCleanupStep(
                                player.Unprepare,
                                "create-failure-unprepare");
                        }
                        finally
                        {
                            RunPlaybackCleanupStep(
                                player.Dispose,
                                "create-failure-dispose");
                        }
                    }

                    Interlocked.Exchange(
                        ref _lastNativePlayerDisposeTimestamp,
                        Stopwatch.GetTimestamp());
                    LogPlaybackMemorySnapshot(
                        "after-dispose-create-failure",
                        null,
                        generation);
                }

                throw;
            }
        }

        private async Task WaitForHlsManifestReadyAsync(
            string streamUrl,
            bool isHls,
            int generation,
            CancellationToken cancellationToken)
        {
            if (!isHls)
                return;

            using var readinessCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
            readinessCancellation.CancelAfter(
                AppState.HlsManifestProbeTimeoutMs);
            var readinessToken = readinessCancellation.Token;
            var stopwatch = Stopwatch.StartNew();
            Exception lastError = null;
            int? lastStatusCode = null;
            int lastManifestBytes = 0;
            while (stopwatch.ElapsedMilliseconds < AppState.HlsManifestProbeTimeoutMs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (readinessToken.IsCancellationRequested)
                    break;
                if (!IsCurrentPlaybackGeneration(generation))
                    throw new OperationCanceledException(cancellationToken);

                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, streamUrl);
                    AppState.Jellyfin?.ApplyAuthorizationHeader(
                        request.Headers,
                        AppState.AccessToken);
                    using var response = await AppState.HttpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        readinessToken);
                    lastStatusCode = (int)response.StatusCode;

                    if (response.IsSuccessStatusCode)
                    {
                        using var responseStream =
                            await response.Content.ReadAsStreamAsync(readinessToken);
                        var buffer = new byte[HlsManifestProbeMaxBytes];
                        int totalBytesRead = 0;
                        while (totalBytesRead < buffer.Length)
                        {
                            int bytesRead = await responseStream.ReadAsync(
                                buffer,
                                totalBytesRead,
                                buffer.Length - totalBytesRead,
                                readinessToken);
                            if (bytesRead <= 0)
                                break;

                            totalBytesRead += bytesRead;
                            lastManifestBytes = totalBytesRead;
                            if (totalBytesRead >= 7)
                            {
                                string prefix = Encoding.UTF8.GetString(
                                    buffer,
                                    0,
                                    totalBytesRead);
                                if (prefix.IndexOf(
                                        "#EXTM3U",
                                        StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    LogPlaybackDiagnostic(
                                        "HlsManifest.Ready",
                                        $"generation={generation},elapsedMs={stopwatch.ElapsedMilliseconds},status={lastStatusCode},bytes={totalBytesRead}");
                                    return;
                                }
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (
                    cancellationToken.IsCancellationRequested ||
                    !IsCurrentPlaybackGeneration(generation))
                {
                    throw;
                }
                catch (OperationCanceledException) when (
                    readinessToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }

                int remainingMs =
                    AppState.HlsManifestProbeTimeoutMs -
                    (int)stopwatch.ElapsedMilliseconds;
                if (remainingMs <= 0)
                    break;

                try
                {
                    await Task.Delay(
                        Math.Min(
                            AppState.HlsManifestProbeIntervalMs,
                            remainingMs),
                        readinessToken);
                }
                catch (OperationCanceledException) when (
                    cancellationToken.IsCancellationRequested ||
                    !IsCurrentPlaybackGeneration(generation))
                {
                    throw;
                }
                catch (OperationCanceledException) when (
                    readinessToken.IsCancellationRequested)
                {
                    break;
                }
            }

            string lastErrorName = lastError?.GetType().Name ?? "manifest-not-ready";
            LogPlaybackDiagnostic(
                "HlsManifest.Timeout",
                $"generation={generation},elapsedMs={stopwatch.ElapsedMilliseconds},status={lastStatusCode?.ToString() ?? "none"},bytes={lastManifestBytes},last={lastErrorName}");
            throw new TimeoutException(
                "The HLS playlist was not ready before the HLS startup timeout.");
        }

        private static bool IsHlsStream(
            string streamUrl,
            MediaSourceInfo mediaSource)
        {
            if (string.Equals(
                    mediaSource?.TranscodingSubProtocol,
                    "hls",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(streamUrl))
                return false;

            return streamUrl.IndexOf(
                ".m3u8",
                StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void LogPlaybackRouteDiagnostics(
            string streamUrl,
            MediaSourceInfo mediaSource,
            string reportedPlayMethod,
            int generation)
        {
            if (!PlaybackDiagnosticsEnabled || mediaSource == null)
                return;

            var video = mediaSource.MediaStreams?
                .FirstOrDefault(s =>
                    string.Equals(s.Type, "Video", StringComparison.OrdinalIgnoreCase));
            int? selectedAudioIndex =
                _overrideAudioIndex ?? mediaSource.DefaultAudioStreamIndex;
            var audio = mediaSource.MediaStreams?
                .FirstOrDefault(s =>
                    string.Equals(s.Type, "Audio", StringComparison.OrdinalIgnoreCase) &&
                    (!selectedAudioIndex.HasValue || s.Index == selectedAudioIndex.Value));
            audio ??= mediaSource.MediaStreams?
                .FirstOrDefault(s =>
                    string.Equals(s.Type, "Audio", StringComparison.OrdinalIgnoreCase));

            string reasons =
                mediaSource.TranscodingReasons == null ||
                mediaSource.TranscodingReasons.Count == 0
                    ? "-"
                    : string.Join(",", mediaSource.TranscodingReasons);
            string urlType = IsHlsStream(streamUrl, mediaSource)
                ? "HLS"
                : ResolveSanitizedUrlType(streamUrl);
            string extension = ResolveSanitizedUrlExtension(streamUrl);
            bool isDirectPlay = string.Equals(
                reportedPlayMethod,
                "DirectPlay",
                StringComparison.OrdinalIgnoreCase);
            bool videoIsCopied =
                !isDirectPlay &&
                (string.Equals(
                     reportedPlayMethod,
                     "DirectStream",
                     StringComparison.OrdinalIgnoreCase) ||
                 LooksLikeDirectStream(mediaSource));
            bool hasAudioTranscodeReason =
                mediaSource.TranscodingReasons?.Any(reason =>
                    !string.IsNullOrWhiteSpace(reason) &&
                    reason.IndexOf(
                        "Audio",
                        StringComparison.OrdinalIgnoreCase) >= 0) == true;
            string requestedVideoCodecs =
                GetQueryParamValue(
                    mediaSource.TranscodingUrl,
                    "VideoCodec");
            string requestedAudioCodecs =
                GetQueryParamValue(
                    mediaSource.TranscodingUrl,
                    "AudioCodec");
            string sourceAudioCodec =
                NormalizeAudioCodec(audio?.Codec);
            bool targetContainsSourceAudio =
                !string.IsNullOrWhiteSpace(sourceAudioCodec) &&
                !string.IsNullOrWhiteSpace(requestedAudioCodecs) &&
                requestedAudioCodecs
                    .Split(',')
                    .Any(codec => string.Equals(
                        NormalizeAudioCodec(codec),
                        sourceAudioCodec,
                        StringComparison.OrdinalIgnoreCase));
            bool audioTargetRequiresEncode =
                !string.IsNullOrWhiteSpace(requestedAudioCodecs) &&
                !string.IsNullOrWhiteSpace(sourceAudioCodec) &&
                !targetContainsSourceAudio;
            string videoHandling = isDirectPlay
                ? "direct"
                : videoIsCopied
                    ? "copy"
                    : "encode";
            string audioHandling = isDirectPlay
                ? "direct"
                : hasAudioTranscodeReason || audioTargetRequiresEncode
                    ? "encode"
                    : "copy-or-remux";

            LogPlaybackDiagnostic(
                "Playback.Route",
                $"generation={generation},source={ShortPlaybackId(mediaSource.Id)}," +
                $"audioIndex={selectedAudioIndex?.ToString(CultureInfo.InvariantCulture) ?? "-"}," +
                $"video={video?.Codec ?? "-"}," +
                $"videoSize={video?.Width?.ToString(CultureInfo.InvariantCulture) ?? "-"}x" +
                $"{video?.Height?.ToString(CultureInfo.InvariantCulture) ?? "-"}," +
                $"videoRange={video?.VideoRange ?? "-"}," +
                $"videoBitrate={video?.BitRate?.ToString(CultureInfo.InvariantCulture) ?? "-"}," +
                $"audio={audio?.Codec ?? "-"}," +
                $"audioChannels={audio?.Channels?.ToString(CultureInfo.InvariantCulture) ?? "-"}," +
                $"supportsDirectPlay={mediaSource.SupportsDirectPlay}," +
                $"supportsDirectStream={mediaSource.SupportsDirectStream}," +
                $"supportsTranscoding={mediaSource.SupportsTranscoding}," +
                $"internalMethod={_playMethod},reportedMethod={reportedPlayMethod}," +
                $"transcodingContainer={mediaSource.TranscodingContainer ?? "-"}," +
                $"transcodingSubProtocol={mediaSource.TranscodingSubProtocol ?? "-"}," +
                $"transcodingReasons={reasons},videoHandling={videoHandling}," +
                $"audioHandling={audioHandling}," +
                $"targetVideo={(!string.IsNullOrWhiteSpace(requestedVideoCodecs) ? requestedVideoCodecs : "-")}," +
                $"targetAudio={(!string.IsNullOrWhiteSpace(requestedAudioCodecs) ? requestedAudioCodecs : "-")}," +
                $"urlType={urlType},extension={extension}");
        }

        private static string ResolveSanitizedUrlType(string streamUrl)
        {
            if (!Uri.TryCreate(streamUrl, UriKind.Absolute, out var uri))
                return "relative";

            if (string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
                return "loopback-http";
            return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                ? "https"
                : string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                    ? "http"
                    : "other";
        }

        private static string ResolveSanitizedUrlExtension(string streamUrl)
        {
            if (string.IsNullOrWhiteSpace(streamUrl))
                return "-";

            try
            {
                if (Uri.TryCreate(streamUrl, UriKind.Absolute, out var uri))
                {
                    string extension = Path.GetExtension(uri.AbsolutePath);
                    if (!string.IsNullOrWhiteSpace(extension))
                        return extension.ToLowerInvariant();
                }

                int queryStart = streamUrl.IndexOf('?');
                string path = queryStart >= 0
                    ? streamUrl.Substring(0, queryStart)
                    : streamUrl;
                string fallbackExtension = Path.GetExtension(path);
                return string.IsNullOrWhiteSpace(fallbackExtension)
                    ? "-"
                    : fallbackExtension.ToLowerInvariant();
            }
            catch
            {
                return "-";
            }
        }

        private async Task<bool> SeekCurrentPlayerAsync(
            int targetMs,
            string stage,
            int visualHoldMs = 0)
        {
            int requestedGeneration = Volatile.Read(ref _playbackToken);
            var cancellationToken = CurrentPlaybackCancellationToken;
            bool gateAcquired = false;
            Interlocked.Increment(ref _playbackTransitionCount);
            try
            {
                if (visualHoldMs > 0)
                    await Task.Delay(visualHoldMs, cancellationToken);

                await _playbackTransitionGate.WaitAsync(cancellationToken);
                gateAcquired = true;

                var player = _player;
                if (!IsCurrentPlayer(player, requestedGeneration) ||
                    IsStopRequested())
                {
                    return false;
                }

                bool wasPlayingBeforeSeek = false;
                try
                {
                    wasPlayingBeforeSeek = player.State == PlayerState.Playing;
                }
                catch
                {
                    wasPlayingBeforeSeek = _playbackShouldBePlaying;
                }

                return await SeekPlayerUnderGateAsync(
                    player,
                    requestedGeneration,
                    targetMs,
                    wasPlayingBeforeSeek,
                    stage,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (
                cancellationToken.IsCancellationRequested ||
                !IsCurrentPlaybackGeneration(requestedGeneration))
            {
                return false;
            }
            finally
            {
                if (gateAcquired)
                    _playbackTransitionGate.Release();
                Interlocked.Decrement(ref _playbackTransitionCount);
            }
        }

        private async Task<bool> SeekPlayerUnderGateAsync(
            Player player,
            int generation,
            int targetMs,
            bool wasPlayingBeforeSeek,
            string stage,
            CancellationToken cancellationToken)
        {
            if (Interlocked.CompareExchange(
                    ref _nativeSeekInProgress,
                    1,
                    0) != 0)
            {
                LogPlaybackDiagnostic(
                    $"{stage}.Ignored",
                    $"generation={generation},reason=seek-already-in-progress");
                return false;
            }

            CancelAndDisposeCancellation(
                Interlocked.Exchange(ref _seekSafetyCancellation, null));

            var stopwatch = Stopwatch.StartNew();
            try
            {
                await player.SetPlayPositionAsync(Math.Max(0, targetMs), false);
                if (!IsCurrentPlayer(player, generation))
                    return false;

                _lastKnownPositionTicks = Math.Max(0, (long)targetMs) * 10000;
                LogPlaybackDiagnostic(
                    $"{stage}.Complete",
                    $"generation={generation},targetMs={targetMs},elapsedMs={stopwatch.ElapsedMilliseconds},route={_reportedPlayMethod}");
                ArmSeekSafetyCheck(
                    player,
                    generation,
                    wasPlayingBeforeSeek);
                return true;
            }
            catch (Exception ex) when (
                ex is not OperationCanceledException ||
                !cancellationToken.IsCancellationRequested)
            {
                LogPlaybackDiagnostic(
                    $"{stage}.Failed",
                    $"generation={generation},targetMs={targetMs},elapsedMs={stopwatch.ElapsedMilliseconds},error={ex.GetType().Name}");
                if (IsCurrentPlayer(player, generation))
                {
                    ArmSeekSafetyCheck(
                        player,
                        generation,
                        wasPlayingBeforeSeek);
                }

                return false;
            }
            finally
            {
                Interlocked.Exchange(ref _nativeSeekInProgress, 0);
            }
        }

        private async Task WaitForHlsInitialSeekReadyAsync(
            Player player,
            int generation,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds <
                   AppState.HlsInitialSeekReadyTimeoutMs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsCurrentPlayer(player, generation))
                    throw new OperationCanceledException(cancellationToken);

                try
                {
                    if (player.State == PlayerState.Playing &&
                        player.GetPlayPosition() > 0)
                    {
                        LogPlaybackDiagnostic(
                            "HlsSeek.Ready",
                            $"generation={generation},elapsedMs={stopwatch.ElapsedMilliseconds}");
                        return;
                    }
                }
                catch
                {
                }

                int remainingMs =
                    AppState.HlsInitialSeekReadyTimeoutMs -
                    (int)stopwatch.ElapsedMilliseconds;
                if (remainingMs <= 0)
                    break;

                await Task.Delay(
                    Math.Min(
                        AppState.HlsInitialSeekReadyPollMs,
                        remainingMs),
                    cancellationToken);
            }

            // Some firmware does not advance its public position until the
            // first seek. Continue with the awaited seek after the bounded
            // readiness window rather than leaving resume stuck at zero.
            LogPlaybackDiagnostic(
                "HlsSeek.ReadyTimeout",
                $"generation={generation},elapsedMs={stopwatch.ElapsedMilliseconds}");
        }

        private void ArmSeekSafetyCheck(
            Player player,
            int generation,
            bool shouldBePlaying)
        {
            if (!shouldBePlaying || !IsCurrentPlayer(player, generation))
                return;

            var sessionCancellation = Volatile.Read(ref _playbackSessionCancellation);
            if (sessionCancellation == null)
                return;

            CancellationTokenSource safetyCancellation;
            try
            {
                safetyCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        sessionCancellation.Token);
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            CancelAndDisposeCancellation(
                Interlocked.Exchange(
                    ref _seekSafetyCancellation,
                    safetyCancellation));
            FireAndForget(
                RunSeekSafetyCheckAsync(
                    player,
                    generation,
                    safetyCancellation.Token),
                nameof(RunSeekSafetyCheckAsync));
        }

        private async Task RunSeekSafetyCheckAsync(
            Player player,
            int generation,
            CancellationToken cancellationToken)
        {
            bool gateAcquired = false;
            try
            {
                await Task.Delay(SeekSafetyNetMs, cancellationToken);
                await _playbackTransitionGate.WaitAsync(cancellationToken);
                gateAcquired = true;

                if (!IsCurrentPlayer(player, generation) ||
                    !_playbackShouldBePlaying)
                {
                    return;
                }

                PlayerState state;
                try
                {
                    state = player.State;
                }
                catch
                {
                    return;
                }

                if (state == PlayerState.Playing)
                    return;

                player.Start();
                LogPlaybackDiagnostic(
                    "Seek.SafetyResume",
                    $"generation={generation},state={state}");
            }
            catch (OperationCanceledException) when (
                cancellationToken.IsCancellationRequested ||
                !IsCurrentPlaybackGeneration(generation))
            {
            }
            finally
            {
                if (gateAcquired)
                    _playbackTransitionGate.Release();
            }
        }

        private void CancelSeekSafetyCheck()
        {
            CancelAndDisposeCancellation(
                Interlocked.Exchange(ref _seekSafetyCancellation, null));
        }

        private void MaybeLogFirstPlaybackPosition(
            Player player,
            int generation,
            int positionMs)
        {
            if (_firstPlaybackPositionLogged ||
                positionMs <= 0 ||
                !IsCurrentPlayer(player, generation))
            {
                return;
            }

            _firstPlaybackPositionLogged = true;
            long elapsedMs = GetElapsedMilliseconds(_playbackStartupTimestamp);
            LogPlaybackDiagnostic(
                "Playback.FirstPosition",
                $"generation={generation},positionMs={positionMs},firstFrameMs={elapsedMs}");
            LogPlaybackMemorySnapshot(
                "after-first-position",
                player,
                generation);
        }

        private void StartFirstPlaybackPositionObserver(
            Player player,
            int generation,
            CancellationToken cancellationToken)
        {
            FireAndForget(
                ObserveFirstPlaybackPositionAsync(
                    player,
                    generation,
                    cancellationToken),
                nameof(ObserveFirstPlaybackPositionAsync));
        }

        private async Task ObserveFirstPlaybackPositionAsync(
            Player player,
            int generation,
            CancellationToken cancellationToken)
        {
            const int pollingIntervalMs = 100;
            const int observationWindowMs = 30000;
            int elapsedMs = 0;

            try
            {
                while (elapsedMs < observationWindowMs &&
                       !_firstPlaybackPositionLogged)
                {
                    await Task.Delay(
                        pollingIntervalMs,
                        cancellationToken);
                    elapsedMs += pollingIntervalMs;
                    if (!IsCurrentPlayer(player, generation))
                        return;
                    if (Volatile.Read(ref _nativeSeekInProgress) != 0)
                        continue;

                    PlayerState state;
                    int positionMs;
                    try
                    {
                        state = player.State;
                        positionMs = player.GetPlayPosition();
                    }
                    catch
                    {
                        continue;
                    }

                    if (state != PlayerState.Playing &&
                        state != PlayerState.Paused)
                    {
                        continue;
                    }

                    MaybeLogFirstPlaybackPosition(
                        player,
                        generation,
                        positionMs);
                }
            }
            catch (OperationCanceledException) when (
                cancellationToken.IsCancellationRequested ||
                !IsCurrentPlaybackGeneration(generation))
            {
            }
        }

        private void LogPlaybackMemorySnapshot(
            string stage,
            Player player,
            int generation,
            bool allowNativeStateRead = true)
        {
            if (!PlaybackDiagnosticsEnabled)
                return;

            long managedBytes = GC.GetTotalMemory(forceFullCollection: false);
            string vmRss = "-";
            string rssAnon = "-";
            try
            {
                if (File.Exists("/proc/self/status"))
                {
                    foreach (var line in File.ReadLines("/proc/self/status"))
                    {
                        if (line.StartsWith("VmRSS:", StringComparison.Ordinal))
                            vmRss = line.Substring("VmRSS:".Length).Trim();
                        else if (line.StartsWith("RssAnon:", StringComparison.Ordinal))
                            rssAnon = line.Substring("RssAnon:".Length).Trim();
                    }
                }
            }
            catch
            {
            }

            string playerState = "none";
            if (player != null && allowNativeStateRead)
            {
                try
                {
                    playerState = player.State.ToString();
                }
                catch
                {
                    playerState = "unavailable";
                }
            }
            else if (player != null)
            {
                playerState = "preempting";
            }

            LogPlaybackDiagnostic(
                $"Memory.{stage}",
                $"generation={generation},playerState={playerState},managedBytes={managedBytes},VmRSS={vmRss},RssAnon={rssAnon}");
        }

        private bool PlaybackDiagnosticsEnabled =>
            DebugSwitches.EnableVerboseDebugLogging ||
            DebugSwitches.EnablePlaybackDebugOverlay;

        private void LogPlaybackDiagnostic(string stage, string details)
        {
            if (!PlaybackDiagnosticsEnabled)
                return;

            string safeStage = string.IsNullOrWhiteSpace(stage)
                ? "Playback"
                : stage.Trim();
            string safeDetails = string.IsNullOrWhiteSpace(details)
                ? "-"
                : SensitiveDataRedactor.Redact(details.Trim());
            string message = $"{safeStage}: {safeDetails}";

            try
            {
                Tizen.Log.Info("PlaybackLifecycle", message);
            }
            catch
            {
            }

            AppDebugLog.Add($"[PlaybackLifecycle] {message}");
            if (DebugSwitches.EnablePlaybackDebugOverlay && !IsScreenHidden)
            {
                RunOnUiThread(() =>
                {
                    if (!IsScreenHidden)
                        CaptureStreamDebugEvent(safeStage, safeDetails);
                });
            }
        }

        private static long GetElapsedMilliseconds(long startTimestamp)
        {
            if (startTimestamp <= 0)
                return 0;

            long elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
            return (long)(elapsedTicks * 1000.0 / Stopwatch.Frequency);
        }

        private async Task ObservePlaybackStopAsync(
            Task<PlaybackStopResult> stopTask,
            PlaybackStopReason reason)
        {
            if (stopTask == null)
                return;

            try
            {
                var result = await stopTask;
                LogPlaybackDiagnostic(
                    "StopReport.Complete",
                    $"reason={reason},outcome={result?.Outcome.ToString() ?? "-"}");
            }
            catch (Exception ex)
            {
                LogPlaybackDiagnostic(
                    "StopReport.Failed",
                    $"reason={reason},error={ex.GetType().Name}");
            }
        }
    }
}
