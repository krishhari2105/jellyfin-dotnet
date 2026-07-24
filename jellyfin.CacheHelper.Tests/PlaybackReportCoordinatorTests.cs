using System.Collections.Concurrent;
using JellyfinTizen.Core;
using Xunit;

namespace Jellyfin.CacheHelper.Tests;

public class PlaybackReportCoordinatorTests
{
    [Theory]
    [InlineData("Jellyfin")]
    [InlineData("Emby")]
    public async Task NormalBack_SendsOneFinalStopWithCompletePayload_AndRejectsLaterProgress(string serverMode)
    {
        var observed = new ConcurrentQueue<ObservedReport>();
        var coordinator = CreateCoordinator((type, snapshot, _) =>
        {
            observed.Enqueue(new ObservedReport(serverMode, type, snapshot));
            return Task.CompletedTask;
        });
        coordinator.BeginGeneration(12);

        Assert.True(coordinator.TryQueueReport(
            PlaybackReportType.Started,
            Snapshot(12, 30_400_000_000, "TimeUpdate"),
            out var started));
        await started;
        Assert.True(coordinator.TryQueueReport(
            PlaybackReportType.Progress,
            Snapshot(12, 30_480_000_000, "TimeUpdate"),
            out var progress));
        await progress;

        var stop = coordinator.RequestStop(
            12,
            PlaybackStopReason.Back,
            () => Snapshot(12, 30_480_000_000, "Stop"));
        var result = await stop.Completion;

        Assert.Equal(PlaybackReportOutcome.Succeeded, result.Outcome);
        Assert.False(coordinator.TryQueueReport(
            PlaybackReportType.Progress,
            Snapshot(12, 30_490_000_000, "TimeUpdate"),
            out _));

        var reports = observed.ToArray();
        Assert.Equal(
            new[] { PlaybackReportType.Started, PlaybackReportType.Progress, PlaybackReportType.Stopped },
            reports.Select(report => report.Type));
        Assert.Single(reports.Where(report => report.Type == PlaybackReportType.Stopped));

        var final = reports[^1].Snapshot;
        Assert.Equal(30_480_000_000, final.PositionTicks);
        Assert.Equal("item-123456789", final.ItemId);
        Assert.Equal("play-session-123456789", final.PlaySessionId);
        Assert.Equal("media-source-123456789", final.MediaSourceId);
        Assert.Equal("DirectStream", final.PlayMethod);
        Assert.Equal(2, final.AudioStreamIndex);
        Assert.Equal(4, final.SubtitleStreamIndex);
        Assert.Equal(serverMode, reports[^1].ServerMode);
    }

    [Fact]
    public async Task MediaStop_SendsOneStop_AndRejectsLaterProgress()
    {
        var observed = new ConcurrentQueue<ObservedReport>();
        var coordinator = CreateCoordinator((type, snapshot, _) =>
        {
            observed.Enqueue(new ObservedReport("Jellyfin", type, snapshot));
            return Task.CompletedTask;
        });
        coordinator.BeginGeneration(3);

        var stop = coordinator.RequestStop(
            3,
            PlaybackStopReason.MediaStop,
            () => Snapshot(3, 99_000_000, "Stop"));
        await stop.Completion;

        Assert.False(coordinator.TryQueueReport(
            PlaybackReportType.Progress,
            Snapshot(3, 100_000_000, "TimeUpdate"),
            out _));
        Assert.Equal(
            new[] { PlaybackReportType.Stopped },
            observed.Select(report => report.Type));
    }

    [Fact]
    public async Task ScreenHidden_AfterCompletedStop_JoinsWithoutTimeUpdateOrDuplicateStop()
    {
        var observed = new ConcurrentQueue<ObservedReport>();
        var coordinator = CreateCoordinator((type, snapshot, _) =>
        {
            observed.Enqueue(new ObservedReport("Jellyfin", type, snapshot));
            return Task.CompletedTask;
        });
        coordinator.BeginGeneration(8);

        var back = coordinator.RequestStop(
            8,
            PlaybackStopReason.Back,
            () => Snapshot(8, 500_000_000, "Stop"));
        await back.Completion;
        var hidden = coordinator.RequestStop(
            8,
            PlaybackStopReason.ScreenHidden,
            () => Snapshot(8, 501_000_000, "Stop"));
        await hidden.Completion;

        Assert.True(hidden.JoinedExisting);
        Assert.Equal(
            new[] { PlaybackReportType.Stopped },
            observed.Select(report => report.Type));
    }

    [Fact]
    public async Task InFlightProgress_CompletesBeforeStopStarts()
    {
        var events = new ConcurrentQueue<string>();
        var progressEntered = NewSignal();
        var releaseProgress = NewSignal();
        var stopEntered = NewSignal();
        var coordinator = CreateCoordinator(async (type, _, cancellationToken) =>
        {
            if (type == PlaybackReportType.Progress)
            {
                events.Enqueue("ProgressStarted");
                progressEntered.TrySetResult();
                await releaseProgress.Task.WaitAsync(cancellationToken);
                events.Enqueue("ProgressCompleted");
                return;
            }

            if (type == PlaybackReportType.Stopped)
            {
                events.Enqueue("StopStarted");
                stopEntered.TrySetResult();
            }
        });
        coordinator.BeginGeneration(21);

        Assert.True(coordinator.TryQueueReport(
            PlaybackReportType.Progress,
            Snapshot(21, 100_000_000, "TimeUpdate"),
            out _));
        await progressEntered.Task;

        var stop = coordinator.RequestStop(
            21,
            PlaybackStopReason.Back,
            () => Snapshot(21, 101_000_000, "Stop"));
        Assert.False(stopEntered.Task.IsCompleted);

        releaseProgress.TrySetResult();
        await stop.Completion;

        Assert.Equal(
            new[] { "ProgressStarted", "ProgressCompleted", "StopStarted" },
            events);
    }

    [Fact]
    public async Task ProgressRequestedAfterStopGate_IsRejected()
    {
        var observed = new ConcurrentQueue<PlaybackReportType>();
        var stopEntered = NewSignal();
        var releaseStop = NewSignal();
        var coordinator = CreateCoordinator(async (type, _, cancellationToken) =>
        {
            observed.Enqueue(type);
            if (type == PlaybackReportType.Stopped)
            {
                stopEntered.TrySetResult();
                await releaseStop.Task.WaitAsync(cancellationToken);
            }
        });
        coordinator.BeginGeneration(5);

        var stop = coordinator.RequestStop(
            5,
            PlaybackStopReason.Back,
            () => Snapshot(5, 1_000_000, "Stop"));
        await stopEntered.Task;

        Assert.False(coordinator.TryQueueReport(
            PlaybackReportType.Progress,
            Snapshot(5, 2_000_000, "Pause"),
            out _));
        releaseStop.TrySetResult();
        await stop.Completion;

        Assert.Equal(new[] { PlaybackReportType.Stopped }, observed);
    }

    [Fact]
    public async Task BackHidePauseAndTerminateRace_ProducesOneStopAndOneSnapshot()
    {
        var observed = new ConcurrentQueue<PlaybackReportType>();
        var coordinator = CreateCoordinator((type, _, _) =>
        {
            observed.Enqueue(type);
            return Task.CompletedTask;
        });
        coordinator.BeginGeneration(33);
        int captureCount = 0;
        var reasons = new[]
        {
            PlaybackStopReason.Back,
            PlaybackStopReason.ScreenHidden,
            PlaybackStopReason.AppPaused,
            PlaybackStopReason.AppTerminating
        };
        var start = NewSignal();

        var callers = reasons.Select(reason => Task.Run(async () =>
        {
            await start.Task;
            return coordinator.RequestStop(
                33,
                reason,
                () =>
                {
                    Interlocked.Increment(ref captureCount);
                    return Snapshot(33, 777_000_000, "Stop");
                });
        })).ToArray();

        start.TrySetResult();
        var requests = await Task.WhenAll(callers);
        await Task.WhenAll(requests.Select(request => request.Completion));

        Assert.Equal(1, captureCount);
        Assert.Single(observed.Where(type => type == PlaybackReportType.Stopped));
        Assert.Equal(3, requests.Count(request => request.JoinedExisting));
        _ = Assert.Single(requests.Select(request => request.Completion).Distinct());
    }

    [Fact]
    public async Task BackwardSeek_UsesLowerLivePositionInsteadOfHigherLastKnownPosition()
    {
        PlaybackReportSnapshot finalStop = null;
        var coordinator = CreateCoordinator((type, snapshot, _) =>
        {
            if (type == PlaybackReportType.Stopped)
                finalStop = snapshot;
            return Task.CompletedTask;
        });
        coordinator.BeginGeneration(44);

        const long staleHigherPosition = 37_200_000_000;
        const long livePostSeekPosition = 30_480_000_000;
        long selectedPosition = PlaybackPositionResolver.ResolveFinalPositionTicks(
            staleHigherPosition,
            hasLivePosition: true,
            livePostSeekPosition);
        var stop = coordinator.RequestStop(
            44,
            PlaybackStopReason.Back,
            () => Snapshot(44, selectedPosition, "Stop"));
        await stop.Completion;

        Assert.NotNull(finalStop);
        Assert.Equal(livePostSeekPosition, finalStop.PositionTicks);
        Assert.NotEqual(staleHigherPosition, finalStop.PositionTicks);
    }

    [Fact]
    public async Task TerminationDisposal_WaitsUntilStopAttemptCompletes()
    {
        var events = new ConcurrentQueue<string>();
        var stopEntered = NewSignal();
        var releaseStop = NewSignal();
        int networkDisposed = 0;
        var coordinator = CreateCoordinator(async (type, _, cancellationToken) =>
        {
            if (type != PlaybackReportType.Stopped)
                return;

            Assert.Equal(0, Volatile.Read(ref networkDisposed));
            events.Enqueue("StopStarted");
            stopEntered.TrySetResult();
            await releaseStop.Task.WaitAsync(cancellationToken);
            Assert.Equal(0, Volatile.Read(ref networkDisposed));
            events.Enqueue("StopCompleted");
        });
        coordinator.BeginGeneration(55);

        var stop = coordinator.RequestStop(
            55,
            PlaybackStopReason.AppTerminating,
            () => Snapshot(55, 88_000_000, "Stop"));
        var terminate = Task.Run(async () =>
        {
            await stop.Completion;
            events.Enqueue("NetworkDisposed");
            Interlocked.Exchange(ref networkDisposed, 1);
        });

        await stopEntered.Task;
        Assert.Equal(0, Volatile.Read(ref networkDisposed));
        Assert.False(terminate.IsCompleted);
        releaseStop.TrySetResult();
        await terminate;

        Assert.Equal(
            new[] { "StopStarted", "StopCompleted", "NetworkDisposed" },
            events);
    }

    [Fact]
    public async Task LifecycleFlushCancellation_DrainsTheNetworkAttemptBeforeCleanup()
    {
        var stopEntered = NewSignal();
        int networkAttemptActive = 0;
        var coordinator = new PlaybackReportCoordinator(
            async (type, _, cancellationToken) =>
            {
                if (type != PlaybackReportType.Stopped)
                    return;

                Interlocked.Exchange(ref networkAttemptActive, 1);
                stopEntered.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                finally
                {
                    Interlocked.Exchange(ref networkAttemptActive, 0);
                }
            },
            TimeSpan.FromSeconds(30));
        coordinator.BeginGeneration(56);

        var stop = coordinator.RequestStop(
            56,
            PlaybackStopReason.AppTerminating,
            () => Snapshot(56, 89_000_000, "Stop"));
        await stopEntered.Task;
        Assert.Equal(1, Volatile.Read(ref networkAttemptActive));

        coordinator.CancelPendingReports();
        var result = await stop.Completion.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(PlaybackReportOutcome.TimedOut, result.Outcome);
        Assert.Equal(0, Volatile.Read(ref networkAttemptActive));
    }

    [Theory]
    [InlineData(false, PlaybackReportOutcome.Failed)]
    [InlineData(true, PlaybackReportOutcome.TimedOut)]
    public async Task StopFailureOrTimeout_StillAllowsDeterministicCleanup(
        bool timeOut,
        PlaybackReportOutcome expectedOutcome)
    {
        var events = new ConcurrentQueue<string>();
        var coordinator = new PlaybackReportCoordinator(
            async (type, _, cancellationToken) =>
            {
                if (type != PlaybackReportType.Stopped)
                    return;

                events.Enqueue("StopAttempted");
                if (timeOut)
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    return;
                }

                throw new HttpRequestException("simulated report failure");
            },
            TimeSpan.FromMilliseconds(40));
        coordinator.BeginGeneration(66);

        var stop = coordinator.RequestStop(
            66,
            PlaybackStopReason.AppTerminating,
            () => Snapshot(66, 123_000_000, "Stop"));
        var result = await stop.Completion;
        events.Enqueue("LocalCleanup");

        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.Equal(new[] { "StopAttempted", "LocalCleanup" }, events);
    }

    [Theory]
    [InlineData(PlaybackStopReason.PlaybackCompleted)]
    [InlineData(PlaybackStopReason.EpisodeSwitch)]
    public async Task CompletionAndEpisodeSwitch_DoNotSendPartialPlaybackStop(PlaybackStopReason reason)
    {
        var observed = new ConcurrentQueue<PlaybackReportType>();
        var coordinator = CreateCoordinator((type, _, _) =>
        {
            observed.Enqueue(type);
            return Task.CompletedTask;
        });
        coordinator.BeginGeneration(77);

        Assert.True(coordinator.TryQueueReport(
            PlaybackReportType.Progress,
            Snapshot(77, 900_000_000, "TimeUpdate"),
            out var progress));
        await progress;
        var completed = coordinator.CompleteWithoutStop(77, reason);
        var result = await completed.Completion;

        Assert.Equal(PlaybackReportOutcome.Skipped, result.Outcome);
        Assert.DoesNotContain(PlaybackReportType.Stopped, observed);
        Assert.False(coordinator.TryQueueReport(
            PlaybackReportType.Progress,
            Snapshot(77, 901_000_000, "TimeUpdate"),
            out _));

        coordinator.BeginGeneration(78);
        Assert.True(coordinator.TryQueueReport(
            PlaybackReportType.Started,
            Snapshot(78, 0, "TimeUpdate"),
            out var nextStarted));
        await nextStarted;
        Assert.Equal(PlaybackReportType.Started, observed.Last());
        Assert.DoesNotContain(PlaybackReportType.Stopped, observed);
    }

    private static PlaybackReportCoordinator CreateCoordinator(
        Func<PlaybackReportType, PlaybackReportSnapshot, CancellationToken, Task> sender)
    {
        return new PlaybackReportCoordinator(sender, TimeSpan.FromSeconds(2));
    }

    private static PlaybackReportSnapshot Snapshot(int generation, long positionTicks, string eventName)
    {
        return new PlaybackReportSnapshot(
            generation,
            "item-123456789",
            "play-session-123456789",
            "media-source-123456789",
            "DirectStream",
            positionTicks,
            isPaused: eventName == "Pause",
            eventName,
            audioStreamIndex: 2,
            subtitleStreamIndex: 4);
    }

    private static TaskCompletionSource NewSignal()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed record ObservedReport(
        string ServerMode,
        PlaybackReportType Type,
        PlaybackReportSnapshot Snapshot);
}
