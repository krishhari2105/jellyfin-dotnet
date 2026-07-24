using System;
using System.Threading;
using System.Threading.Tasks;
using JellyfinTizen.Models;

namespace JellyfinTizen.Core
{
    public enum PlaybackReportType
    {
        Started,
        Progress,
        Stopped
    }

    public enum PlaybackStopReason
    {
        Back,
        MediaStop,
        ScreenHidden,
        AppPaused,
        AppTerminating,
        PlaybackCompleted,
        EpisodeSwitch,
        PlaybackRestart
    }

    public enum PlaybackReportOutcome
    {
        Succeeded,
        Failed,
        TimedOut,
        Skipped
    }

    public static class PlaybackPositionResolver
    {
        public static long ResolveFinalPositionTicks(
            long lastKnownPositionTicks,
            bool hasLivePosition,
            long livePositionTicks)
        {
            long selectedPosition = hasLivePosition ? livePositionTicks : lastKnownPositionTicks;
            return selectedPosition < 0 ? 0 : selectedPosition;
        }
    }

    /// <summary>
    /// Immutable playback-report data captured while the native player and reporting
    /// context are still valid.
    /// </summary>
    public sealed class PlaybackReportSnapshot
    {
        public PlaybackReportSnapshot(
            int generation,
            string itemId,
            string playSessionId,
            string mediaSourceId,
            string playMethod,
            long positionTicks,
            bool isPaused,
            string eventName,
            int? audioStreamIndex,
            int? subtitleStreamIndex)
        {
            Generation = generation;
            ItemId = itemId;
            PlaySessionId = playSessionId;
            MediaSourceId = mediaSourceId;
            PlayMethod = string.IsNullOrWhiteSpace(playMethod) ? "DirectPlay" : playMethod;
            PositionTicks = Math.Max(0, positionTicks);
            IsPaused = isPaused;
            EventName = eventName;
            AudioStreamIndex = audioStreamIndex;
            SubtitleStreamIndex = subtitleStreamIndex;
        }

        public int Generation { get; }

        public string ItemId { get; }

        public string PlaySessionId { get; }

        public string MediaSourceId { get; }

        public string PlayMethod { get; }

        public long PositionTicks { get; }

        public bool IsPaused { get; }

        public string EventName { get; }

        public int? AudioStreamIndex { get; }

        public int? SubtitleStreamIndex { get; }

        public PlaybackProgressInfo ToProgressInfo()
        {
            return new PlaybackProgressInfo
            {
                ItemId = ItemId,
                PlaySessionId = PlaySessionId,
                MediaSourceId = MediaSourceId,
                PlayMethod = PlayMethod,
                PositionTicks = PositionTicks,
                IsPaused = IsPaused,
                EventName = EventName,
                AudioStreamIndex = AudioStreamIndex,
                SubtitleStreamIndex = SubtitleStreamIndex
            };
        }
    }

    public sealed class PlaybackStopResult
    {
        public PlaybackStopResult(
            PlaybackStopReason reason,
            PlaybackReportOutcome outcome,
            PlaybackReportSnapshot snapshot)
        {
            Reason = reason;
            Outcome = outcome;
            Snapshot = snapshot;
        }

        public PlaybackStopReason Reason { get; }

        public PlaybackReportOutcome Outcome { get; }

        public PlaybackReportSnapshot Snapshot { get; }
    }

    public readonly struct PlaybackStopRequest
    {
        public PlaybackStopRequest(Task<PlaybackStopResult> completion, bool joinedExisting)
        {
            Completion = completion;
            JoinedExisting = joinedExisting;
        }

        public Task<PlaybackStopResult> Completion { get; }

        public bool JoinedExisting { get; }
    }

    /// <summary>
    /// Serializes playback reports for one player screen. A stop request closes the
    /// generation gate synchronously, waits for previously accepted reports, and then
    /// sends at most one authoritative Stopped report.
    /// </summary>
    public sealed class PlaybackReportCoordinator
    {
        private readonly object _sync = new();
        private readonly Func<PlaybackReportType, PlaybackReportSnapshot, CancellationToken, Task> _sendAsync;
        private readonly Action<string> _log;
        private readonly TimeSpan _requestTimeout;
        private readonly CancellationTokenSource _pipelineCancellation = new();
        private Task _reportTail = Task.CompletedTask;
        private Task<PlaybackStopResult> _stopTask;
        private PlaybackReportSnapshot _stopSnapshot;
        private PlaybackStopReason _stopReason;
        private int _activeGeneration = -1;
        private bool _stopRequested;

        public PlaybackReportCoordinator(
            Func<PlaybackReportType, PlaybackReportSnapshot, CancellationToken, Task> sendAsync,
            TimeSpan requestTimeout,
            Action<string> log = null)
        {
            _sendAsync = sendAsync ?? throw new ArgumentNullException(nameof(sendAsync));
            if (requestTimeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(requestTimeout));

            _requestTimeout = requestTimeout;
            _log = log;
        }

        public void BeginGeneration(int generation)
        {
            lock (_sync)
            {
                _activeGeneration = generation;
                _stopRequested = false;
                _stopTask = null;
                _stopSnapshot = null;
            }
        }

        public bool IsStopping(int generation)
        {
            lock (_sync)
            {
                return generation == _activeGeneration && _stopRequested;
            }
        }

        public void CancelPendingReports()
        {
            try
            {
                _pipelineCancellation.Cancel();
            }
            catch (Exception ex)
            {
                WriteLog($"report=pipeline-cancel outcome=Failed detail={ex.GetType().Name}");
            }
        }

        public bool TryQueueReport(
            PlaybackReportType reportType,
            PlaybackReportSnapshot snapshot,
            out Task completion)
        {
            if (reportType == PlaybackReportType.Stopped)
                throw new ArgumentException("Stopped reports must use RequestStop.", nameof(reportType));
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            Task previous;
            TaskCompletionSource<bool> completionSource;
            lock (_sync)
            {
                if (snapshot.Generation != _activeGeneration)
                {
                    LogReport(reportType, snapshot, null, PlaybackReportOutcome.Skipped, "generation-mismatch");
                    completion = Task.CompletedTask;
                    return false;
                }

                if (_stopRequested)
                {
                    LogReport(reportType, snapshot, _stopReason, PlaybackReportOutcome.Skipped, "stopping");
                    completion = Task.CompletedTask;
                    return false;
                }

                previous = _reportTail;
                completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                completion = completionSource.Task;
                _reportTail = completion;
            }

            _ = CompleteQueuedReportAsync(completionSource, previous, reportType, snapshot);
            return true;
        }

        public PlaybackStopRequest RequestStop(
            int generation,
            PlaybackStopReason reason,
            Func<PlaybackReportSnapshot> captureSnapshot)
        {
            if (captureSnapshot == null)
                throw new ArgumentNullException(nameof(captureSnapshot));

            return BeginStop(generation, reason, captureSnapshot, sendStoppedReport: true);
        }

        public PlaybackStopRequest CompleteWithoutStop(int generation, PlaybackStopReason reason)
        {
            return BeginStop(generation, reason, captureSnapshot: null, sendStoppedReport: false);
        }

        private PlaybackStopRequest BeginStop(
            int generation,
            PlaybackStopReason reason,
            Func<PlaybackReportSnapshot> captureSnapshot,
            bool sendStoppedReport)
        {
            Task previous;
            TaskCompletionSource<PlaybackStopResult> completionSource;
            lock (_sync)
            {
                if (generation != _activeGeneration)
                {
                    var staleResult = new PlaybackStopResult(reason, PlaybackReportOutcome.Skipped, null);
                    LogStop(generation, null, reason, PlaybackReportOutcome.Skipped, "generation-mismatch");
                    return new PlaybackStopRequest(Task.FromResult(staleResult), joinedExisting: false);
                }

                if (_stopRequested && _stopTask != null)
                {
                    LogStop(
                        generation,
                        _stopSnapshot,
                        reason,
                        PlaybackReportOutcome.Skipped,
                        $"joined-existing-{_stopReason}");
                    return new PlaybackStopRequest(_stopTask, joinedExisting: true);
                }

                _stopRequested = true;
                _stopReason = reason;
                previous = _reportTail;
                completionSource = new TaskCompletionSource<PlaybackStopResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                _stopTask = completionSource.Task;
                _reportTail = _stopTask;
            }

            PlaybackReportSnapshot snapshot = null;
            if (sendStoppedReport)
            {
                try
                {
                    snapshot = captureSnapshot();
                }
                catch (Exception ex)
                {
                    LogStop(generation, null, reason, PlaybackReportOutcome.Failed, $"snapshot-{ex.GetType().Name}");
                }
            }

            lock (_sync)
            {
                _stopSnapshot = snapshot;
            }

            _ = CompleteStopAsync(completionSource, previous, generation, reason, snapshot, sendStoppedReport);
            return new PlaybackStopRequest(completionSource.Task, joinedExisting: false);
        }

        private async Task CompleteQueuedReportAsync(
            TaskCompletionSource<bool> completionSource,
            Task previous,
            PlaybackReportType reportType,
            PlaybackReportSnapshot snapshot)
        {
            try
            {
                await previous.ConfigureAwait(false);
                var sendResult = await SendWithTimeoutAsync(reportType, snapshot).ConfigureAwait(false);
                LogReport(reportType, snapshot, null, sendResult.Outcome, sendResult.Detail);
            }
            catch (Exception ex)
            {
                LogReport(reportType, snapshot, null, PlaybackReportOutcome.Failed, $"pipeline-{ex.GetType().Name}");
            }
            finally
            {
                completionSource.TrySetResult(true);
            }
        }

        private async Task CompleteStopAsync(
            TaskCompletionSource<PlaybackStopResult> completionSource,
            Task previous,
            int generation,
            PlaybackStopReason reason,
            PlaybackReportSnapshot snapshot,
            bool sendStoppedReport)
        {
            var outcome = PlaybackReportOutcome.Skipped;
            try
            {
                await previous.ConfigureAwait(false);
                if (sendStoppedReport && snapshot != null)
                {
                    var sendResult = await SendWithTimeoutAsync(PlaybackReportType.Stopped, snapshot).ConfigureAwait(false);
                    outcome = sendResult.Outcome;
                    LogStop(generation, snapshot, reason, outcome, sendResult.Detail);
                    completionSource.TrySetResult(new PlaybackStopResult(reason, outcome, snapshot));
                    return;
                }
            }
            catch (Exception ex)
            {
                outcome = PlaybackReportOutcome.Failed;
                LogStop(generation, snapshot, reason, outcome, $"pipeline-{ex.GetType().Name}");
            }

            LogStop(generation, snapshot, reason, outcome, null);
            completionSource.TrySetResult(new PlaybackStopResult(reason, outcome, snapshot));
        }

        private async Task<SendResult> SendWithTimeoutAsync(
            PlaybackReportType reportType,
            PlaybackReportSnapshot snapshot)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                _pipelineCancellation.Token);
            timeout.CancelAfter(_requestTimeout);
            try
            {
                await _sendAsync(reportType, snapshot, timeout.Token).ConfigureAwait(false);
                return new SendResult(PlaybackReportOutcome.Succeeded, null);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                return new SendResult(PlaybackReportOutcome.TimedOut, "request-timeout");
            }
            catch (Exception ex)
            {
                return new SendResult(PlaybackReportOutcome.Failed, $"request-{ex.GetType().Name}");
            }
        }

        private readonly struct SendResult
        {
            public SendResult(PlaybackReportOutcome outcome, string detail)
            {
                Outcome = outcome;
                Detail = detail;
            }

            public PlaybackReportOutcome Outcome { get; }

            public string Detail { get; }
        }

        private void LogReport(
            PlaybackReportType reportType,
            PlaybackReportSnapshot snapshot,
            PlaybackStopReason? reason,
            PlaybackReportOutcome outcome,
            string detail)
        {
            WriteLog(
                $"generation={snapshot.Generation} report={reportType} event={snapshot.EventName ?? "-"} " +
                $"item={ShortId(snapshot.ItemId)} playSession={ShortId(snapshot.PlaySessionId)} " +
                $"positionTicks={snapshot.PositionTicks} reason={reason?.ToString() ?? "-"} " +
                $"outcome={outcome}{FormatDetail(detail)}");
        }

        private void LogStop(
            int generation,
            PlaybackReportSnapshot snapshot,
            PlaybackStopReason reason,
            PlaybackReportOutcome outcome,
            string detail)
        {
            WriteLog(
                $"generation={generation} report={PlaybackReportType.Stopped} event=Stop " +
                $"item={ShortId(snapshot?.ItemId)} playSession={ShortId(snapshot?.PlaySessionId)} " +
                $"positionTicks={snapshot?.PositionTicks ?? 0} reason={reason} " +
                $"outcome={outcome}{FormatDetail(detail)}");
        }

        private void WriteLog(string message)
        {
            try
            {
                _log?.Invoke(message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Playback lifecycle logger failed: {ex.GetType().Name}");
            }
        }

        private static string ShortId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "-";

            value = value.Trim();
            return value.Length <= 8 ? value : value.Substring(0, 8);
        }

        private static string FormatDetail(string detail)
        {
            return string.IsNullOrWhiteSpace(detail) ? string.Empty : $" detail={detail}";
        }
    }
}
