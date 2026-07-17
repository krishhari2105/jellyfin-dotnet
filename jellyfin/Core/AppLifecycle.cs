using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Tizen.Applications;

namespace JellyfinTizen.Core
{
    public enum AppLifecycleState
    {
        NotStarted,
        ProcessLaunch,
        TailscaleStaging,
        TailscaleStagingSucceeded,
        TailscaleStagingFailed,
        ProxyStarting,
        ProxyListeningConfirmed,
        ProxyStartFailed,
        SavedSessionResumeAttempt,
        SavedSessionResumed,
        SavedSessionResumeFailed,
        ServerOnlyRestore,
        ServerOnlyRestored,
        ServerOnlyRestoreFailed,
        FetchingPublicUsers,
        PublicUsersFetched,
        FetchingPublicUsersFailed,
        UserSelection,
        UserSelected,
        UserSelectionCancelled,
        Authentication,
        Authenticated,
        AuthenticationFailed,
        TailscaleAuthRequired,
        TailscaleAuthWaitingForQR,
        TailscaleAuthPolling,
        TailscaleAuthConnected,
        TailscaleAuthTimeout,
        TailscaleAuthCancelled,
        HomeLoading,
        HomeLibrariesFetched,
        HomeRowsBuilt,
        Ready,
        Suspended,
        Resuming,
        ResumeClearingCaches,
        ResumeRestartingTailscale,
        ResumeTailscaleStagingSucceeded,
        ResumeTailscaleStagingFailed,
        ResumeProxyStarting,
        ResumeProxyListeningConfirmed,
        ResumeProxyStartFailed,
        ResumeWaitingForTailnet,
        ResumeTailnetReconnected,
        ResumeTailnetTimeout,
        ResumeCompleted,
        ResumeFailed,
    }

    public static class AppLifecycle
    {
        private static AppLifecycleState _state = AppLifecycleState.NotStarted;
        private static AppLifecycleState _lastActiveStateBeforeSuspend = AppLifecycleState.NotStarted;

        private static readonly SemaphoreSlim Gate = new(1, 1);
        private static readonly ConcurrentDictionary<AppLifecycleState, TaskCompletionSource<bool>> Waiters = new();

        public static AppLifecycleState State
        {
            get
            {
                Gate.Wait();
                try { return _state; }
                finally { Gate.Release(); }
            }
            private set
            {
                AppLifecycleState old;
                Gate.Wait();
                try
                {
                    old = _state;
                    _state = value;
                    if (Waiters.TryGetValue(value, out var tcs))
                    {
                        Waiters.TryRemove(value, out _);
                        tcs.TrySetResult(true);
                    }
                }
                finally { Gate.Release(); }

                StateChanged?.Invoke(old, value);
            }
        }

        public static event Action<AppLifecycleState, AppLifecycleState> StateChanged;

        public static bool IsResuming => State >= AppLifecycleState.Resuming && State <= AppLifecycleState.ResumeCompleted;

        public static async Task<bool> TryTransitionAsync(
            AppLifecycleState from,
            AppLifecycleState to,
            Func<Task<bool>> precondition = null,
            CancellationToken ct = default)
        {
            await Gate.WaitAsync(ct);
            try
            {
                if (_state != from) return false;
            }
            finally { Gate.Release(); }

            if (precondition != null && !await precondition())
                return false;

            await Gate.WaitAsync(ct);
            try
            {
                if (_state != from) return false;

                _state = to;
                if (Waiters.TryGetValue(to, out var tcs))
                {
                    Waiters.TryRemove(to, out _);
                    tcs.TrySetResult(true);
                }
            }
            finally { Gate.Release(); }

            StateChanged?.Invoke(from, to);
            return true;
        }

        public static void Transition(AppLifecycleState from, AppLifecycleState to)
        {
            _ = TryTransitionAsync(from, to);
        }

        public static void TransitionToSuspended()
        {
            AppLifecycleState previous;
            Gate.Wait();
            try
            {
                _lastActiveStateBeforeSuspend = _state;
                previous = _lastActiveStateBeforeSuspend;
                _state = AppLifecycleState.Suspended;
            }
            finally { Gate.Release(); }

            StateChanged?.Invoke(previous, AppLifecycleState.Suspended);
        }

        public static async Task<bool> WaitForStateAsync(
            AppLifecycleState target,
            TimeSpan timeout,
            CancellationToken ct = default)
        {
            if (State == target) return true;

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!Waiters.TryAdd(target, tcs))
            {
                return await Waiters[target].Task.WaitAsync(timeout, ct);
            }

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(timeout);
                await tcs.Task.WaitAsync(cts.Token);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            finally
            {
                Waiters.TryRemove(target, out _);
            }
        }

        public static async Task<bool> WaitForProxyListenerReadyAsync(int port, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    using var socket = new Socket(
                        AddressFamily.InterNetwork,
                        SocketType.Stream,
                        ProtocolType.Tcp);
                    var connectTask = socket.ConnectAsync("127.0.0.1", port);
                    var completed = await Task.WhenAny(connectTask, Task.Delay(200));
                    if (completed == connectTask && socket.Connected)
                        return true;
                }
                catch { }
                await Task.Delay(50);
            }
            return false;
        }

        public static AppLifecycleState GetResumeTargetState()
        {
            Gate.Wait();
            try { return _lastActiveStateBeforeSuspend; }
            finally { Gate.Release(); }
        }

        public static AppLifecycleState GetPhaseEntryState(AppLifecycleState suspendedState)
        {
            return suspendedState switch
            {
                AppLifecycleState.TailscaleAuthWaitingForQR => AppLifecycleState.TailscaleAuthRequired,
                AppLifecycleState.TailscaleAuthPolling => AppLifecycleState.TailscaleAuthRequired,
                AppLifecycleState.FetchingPublicUsers => AppLifecycleState.ServerOnlyRestored,
                AppLifecycleState.HomeLoading => AppLifecycleState.HomeLoading,
                AppLifecycleState.SavedSessionResumeAttempt => AppLifecycleState.SavedSessionResumeAttempt,
                AppLifecycleState.ServerOnlyRestore => AppLifecycleState.ServerOnlyRestore,
                AppLifecycleState.Authentication => AppLifecycleState.UserSelected,
                AppLifecycleState.UserSelection => AppLifecycleState.UserSelection,
                AppLifecycleState.ProxyStarting => AppLifecycleState.TailscaleStagingSucceeded,
                AppLifecycleState.ProxyListeningConfirmed => AppLifecycleState.ProxyStarting,
                _ => AppLifecycleState.Ready,
            };
        }
    }
}
