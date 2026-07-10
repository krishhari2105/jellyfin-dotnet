using System;
using System.Threading;
using System.Threading.Tasks;

namespace JellyfinTizen.Core
{
    public sealed class TailscaleConnectionMonitor : IDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private bool _disposed;

        public TailscaleConnectionMonitor()
        {
            Task.Run(() => MonitorLoopAsync(_cts.Token));
        }

        private async Task MonitorLoopAsync(CancellationToken ct)
        {
            TailscaleDebugLog.Add("TailscaleConnectionMonitor started.");
            int backoffSeconds = 1;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // Only monitor if the application state is stable (Ready)
                    if (AppLifecycle.State != AppLifecycleState.Ready)
                    {
                        TailscaleDebugLog.Add($"TailscaleConnectionMonitor: Skipping check, app lifecycle state is {AppLifecycle.State}");
                    }
                    else if (AppState.IsTailscaleUrl(AppState.ServerUrl))
                    {
                        bool connected = await AppState.IsTailscaleConnectedAsync();
                        if (!connected)
                        {
                            TailscaleDebugLog.Add($"Tailscale disconnected! Attempting reconnection in {backoffSeconds}s...");
                            await Task.Delay(TimeSpan.FromSeconds(backoffSeconds), ct);

                            // Try to restart daemon/stage and start
                            if (AppState.Tailscale != null)
                            {
                                try
                                {
                                    await AppState.Tailscale.StageAndStart();
                                    bool reconnected = await AppState.Tailscale.WaitForBackendRunningAsync(10000);
                                    if (reconnected)
                                    {
                                        TailscaleDebugLog.Add("Tailscale successfully reconnected.");
                                        backoffSeconds = 1; // Reset backoff
                                    }
                                    else
                                    {
                                        backoffSeconds = Math.Min(backoffSeconds * 2, 60);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    TailscaleDebugLog.Add($"Tailscale reconnection failed: {ex.Message}");
                                    backoffSeconds = Math.Min(backoffSeconds * 2, 60);
                                }
                            }
                        }
                        else
                        {
                            backoffSeconds = 1; // Reset backoff if connected
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    TailscaleDebugLog.Add($"TailscaleConnectionMonitor error in loop: {ex.Message}");
                }

                // Check connection status every 10 seconds under normal operation
                try { await Task.Delay(TimeSpan.FromSeconds(10), ct); } catch { break; }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _cts.Cancel();
            _cts.Dispose();
            _disposed = true;
            TailscaleDebugLog.Add("TailscaleConnectionMonitor stopped and disposed.");
        }
    }
}
