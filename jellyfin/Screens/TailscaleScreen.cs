using System;
using System.Threading;
using System.Threading.Tasks;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using Tizen.NUI.Components;
using JellyfinTizen.Core;
using JellyfinTizen.UI;
using Tizen.Applications;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using System.Linq;

namespace JellyfinTizen.Screens
{
    public class TailscaleScreen : ScreenBase, IKeyHandler
    {
        private View _statusPanel;
        private TextLabel _statusLabel;
        private ImageView _qrImageView;
        private View _loginButton;
        private TextLabel _loginLabel;
        private bool _isLoading;
private bool wasConnected = false;
private CancellationTokenSource _refreshCts;

        public TailscaleScreen()
        {
            Initialize();
        }

        private void Initialize()
        {
            var root = UiFactory.CreateAtmosphericBackground();
            var panel = UiFactory.CreateCenteredPanel(width: 1000, top: 100);
            panel.Add(UiFactory.CreateDisplayTitle("Tailscale"));
            panel.Add(UiFactory.CreateSubtitle("Tailnet connectivity"));

            _statusPanel = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                CornerRadius = 20,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BorderlineWidth = 1.5f,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    CellPadding = new Size2D(0, 16),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                },
                Padding = new Extents(24, 24, 24, 24)
            };

            _statusLabel = new TextLabel("Checking status...")
            {
                TextColor = UiTheme.TextPrimary,
                PointSize = 26,
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word
            };
            _statusPanel.Add(_statusLabel);

            _qrImageView = new ImageView
            {
                WidthSpecification = 320,
                HeightSpecification = 320,
                ExcludeLayouting = true
            };
            _qrImageView.Hide();
            _statusPanel.Add(_qrImageView);

            panel.Add(_statusPanel);

            _loginButton = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 96,
                CornerRadius = 20,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BorderlineWidth = 1.5f,
                Focusable = true
            };
            UiFactory.SetButtonFocusState(_loginButton, focused: false);

            _loginLabel = new TextLabel("Log In with Tailscale")
            {
                TextColor = UiTheme.TextPrimary,
                PointSize = 28,
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _loginButton.Add(_loginLabel);
            panel.Add(_loginButton);

            _loginButton.FocusGained += (s, e) => UiFactory.SetButtonFocusState(_loginButton, focused: true);
            _loginButton.FocusLost += (s, e) => UiFactory.SetButtonFocusState(_loginButton, focused: false);

            root.Add(panel);
            Add(root);
        }

        public override void OnShow()
        {
            base.OnShow();
            ShowDebugOverlay();
            SubscribeAuthUrlEvents();
            TailscaleDebugLog.Add("=== TailscaleScreen shown ===");
            _ = RefreshStatusAsync();

            // Set initial focus to login button if it's focusable
            if (_loginButton.Focusable)
            {
                FocusManager.Instance.SetCurrentFocusView(_loginButton);
            }

            // Start periodic refresh to handle slow Tailscale service startup
            StartPeriodicRefresh();
        }

        public override void OnHide()
        {
            _busCts?.Cancel();
            UnsubscribeAuthUrlEvents();
            base.OnHide(); // calls HideDebugOverlay()
        }



        private async Task RefreshStatusAsync()
        {
            if (_isLoading)
                return;

            _isLoading = true;
            _statusLabel.Text = "Loading...";
            TailscaleDebugLog.Add("RefreshStatusAsync started");

            try
            {
                if (AppState.Tailscale == null)
                {
                    TailscaleDebugLog.Add("ERROR: AppState.Tailscale is null");
                    _statusLabel.Text = "Tailscale is not available.\n\nBuild with tailscaled binary to enable Tailscale support.";
                    _loginButton.Opacity = 0.4f;
                    _loginButton.Focusable = false;
                    _isLoading = false;
                    _qrImageView.Hide();
                    _qrImageView.ExcludeLayouting = true;
                    return;
                }

                TailscaleDebugLog.Add($"Tailscale service exists. IsRunning={AppState.Tailscale.IsRunning}");

                // Retry status check a few times - tailscaled may be running but API not ready
                JsonNode status = null;
                int maxAttempts = 3;
                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    TailscaleDebugLog.Add($"Getting status from tailscaled... (attempt {attempt}/{maxAttempts})");
                    try
                    {
                        status = await AppState.Tailscale.GetStatusAsync();
                        TailscaleDebugLog.Add("Status retrieved successfully");
                        break;
                    }
                    catch (Exception ex)
                    {
                        TailscaleDebugLog.Add($"Attempt {attempt} failed: {ex.GetType().Name}: {ex.Message}");
                        if (attempt < maxAttempts)
                        {
                            TailscaleDebugLog.Add("Waiting 2 seconds before retry...");
                            await Task.Delay(2000);
                        }
                    }
                }

                if (status == null)
                {
                    TailscaleDebugLog.Add("All attempts failed - tailscaled socket not reachable");
                    _statusLabel.Text = "Cannot reach Tailscale daemon.\nCheck logs for startup errors.";
                    _loginButton.Opacity = 0.4f;
                    _loginButton.Focusable = false;
                    _isLoading = false;
                    _qrImageView.Hide();
                    _qrImageView.ExcludeLayouting = true;
                    return;
                }

                // NOTE: 'Running' in Tailscale status means 'node has IPs and is forwarding traffic'.
                // It can be false even when connected (BackendState=Running) during initial auth.
                // Use BackendState to determine if the daemon is actually healthy.
                var running = status?["Running"]?.GetValue<bool>() ?? false;
                var backendState = status?["BackendState"]?.ToString() ?? "Unknown";
                TailscaleDebugLog.Add($"Status: Running={running}, BackendState={backendState}");

                // BackendState values: NoState, NeedsLogin, NeedsMachineAuth, Stopped,
                //                     Starting, Running
                bool daemonHealthy = backendState == "Running" || backendState == "Starting" ||
                                     backendState == "NeedsLogin" || backendState == "NeedsMachineAuth";

                if (!daemonHealthy)
                {
                    TailscaleDebugLog.Add($"Daemon unhealthy, BackendState={backendState}");
                    _statusLabel.Text = $"Tailscale daemon is not running.\nState: {backendState}";
                    _loginButton.Opacity = 1.0f;
                    _loginButton.Focusable = true;
                    _isLoading = false;
                    _qrImageView.Hide();
                    _qrImageView.ExcludeLayouting = true;
                    return;
                }

                var selfNode = status?["Self"]?.AsObject();
                var hostname = selfNode?["HostName"]?.ToString();
                var online = selfNode?["Online"]?.GetValue<bool>() ?? false;
                var ips = selfNode?["TailscaleIPs"]?.AsArray() ?? new JsonArray();

                TailscaleDebugLog.Add($"Self: hostname={hostname}, online={online}, ips={ips.Count}");

                string ipList = "";
                foreach (var ip in ips)
                {
                    if (!string.IsNullOrWhiteSpace(ipList)) ipList += ", ";
                    ipList += ip?.ToString();
                }
                if (string.IsNullOrWhiteSpace(ipList))
                    ipList = "No Tailscale IPs";

                var authUrl = AppState.Tailscale?.LastAuthUrl;
                if (string.IsNullOrWhiteSpace(authUrl))
                    authUrl = ExtractAuthUrl(status);
                string statusText;
                // Consider the daemon as logged in if BackendState is "Running" (has valid key)
                // Even if Online is false temporarily (e.g., no network), we still treat as logged in.
                if (backendState == "Running" || online)
                {
                    var peerRoutes = BuildPeerRouteSummary(status);
                    statusText = $"Connected to tailnet\nHostname: {hostname}\nIPs: {ipList}";
                    if (!string.IsNullOrWhiteSpace(peerRoutes))
                        statusText += $"\nRoutes: {peerRoutes}";
                    TailscaleDebugLog.Add("Status: Connected to tailnet");
                    _loginLabel.Text = "Connected";
                    _loginButton.Opacity = 0.4f;
                    _loginButton.Focusable = false;
                    _qrImageView.Hide();
                    _qrImageView.ExcludeLayouting = true;
                }
                else if (backendState == "NeedsLogin" || !string.IsNullOrEmpty(authUrl))
                {
                    _loginLabel.Text = "Log In with Tailscale";
                    _loginButton.Opacity = 1.0f;
                    _loginButton.Focusable = true;
                    if (!string.IsNullOrEmpty(authUrl))
                    {
                        statusText = $"Open this URL or scan the QR code to authenticate:\n\n{authUrl}";
                        TailscaleDebugLog.Add($"Status: Waiting for login, URL={authUrl}");
                        var qrPath = JellyfinTizen.Utils.QrCodeHelper.GenerateQrCode(authUrl);
                        if (qrPath != null)
                        {
                            _qrImageView.SetImage(qrPath);
                            _qrImageView.ExcludeLayouting = false;
                            _qrImageView.Show();
                        }
                        else
                        {
                            _qrImageView.Hide();
                            _qrImageView.ExcludeLayouting = true;
                        }
                    }
                    else
                    {
                        statusText = $"Not logged in.\nPress 'Log In with Tailscale' to authenticate.";
                        TailscaleDebugLog.Add("Status: NeedsLogin, no authUrl yet");
                        _qrImageView.Hide();
                        _qrImageView.ExcludeLayouting = true;
                    }
                }
                else
                {
                    _loginLabel.Text = "Log In with Tailscale";
                    _loginButton.Opacity = 1.0f;
                    _loginButton.Focusable = true;
                    statusText = $"Tailscale state: {backendState}\nHostname: {hostname}";
                    TailscaleDebugLog.Add($"Status: State={backendState}, not online");
                    _qrImageView.Hide();
                    _qrImageView.ExcludeLayouting = true;
                }

                _statusLabel.Text = statusText;
            }
            catch (Exception ex)
            {
                TailscaleDebugLog.Add($"ERROR: {ex.GetType().Name}: {ex.Message}");
                _statusLabel.Text = $"Error: {ex.Message}";
                _loginButton.Opacity = 0.4f;
                _loginButton.Focusable = false;
                _qrImageView.Hide();
                _qrImageView.ExcludeLayouting = true;
            }
            finally
            {
                _isLoading = false;
                TailscaleDebugLog.Add("RefreshStatusAsync completed");
            }
        }

        private static string BuildPeerRouteSummary(JsonNode status)
        {
            try
            {
                var peers = status?["Peer"]?.AsObject();
                if (peers == null || peers.Count == 0)
                    return string.Empty;

                var targetRows = new List<string>();
                var activeRows = new List<string>();
                var otherRows = new List<string>();
                var targetHost = TryGetTailscaleServerHost();

                foreach (var peerEntry in peers)
                {
                    var peer = peerEntry.Value?.AsObject();
                    if (peer == null)
                        continue;

                    var hostname = peer["HostName"]?.ToString();
                    var dnsName = peer["DNSName"]?.ToString();
                    var curAddr = peer["CurAddr"]?.ToString();
                    var relay = peer["Relay"]?.ToString();
                    var online = peer["Online"]?.GetValue<bool>() ?? false;
                    var active = peer["Active"]?.GetValue<bool>() ?? false;
                    var ips = peer["TailscaleIPs"]?.AsArray();
                    var matchesTarget = MatchesTailscaleHost(ips, targetHost);

                    var name = !string.IsNullOrWhiteSpace(hostname)
                        ? hostname
                        : !string.IsNullOrWhiteSpace(dnsName)
                            ? dnsName.TrimEnd('.')
                            : peerEntry.Key;
                    var route = !string.IsNullOrWhiteSpace(curAddr)
                        ? $"direct {curAddr}"
                        : !string.IsNullOrWhiteSpace(relay)
                            ? $"relay {relay}"
                            : "idle";
                    var flags = active ? "active" : online ? "online" : "offline";
                    var prefix = matchesTarget ? "* " : "";
                    var row = $"{prefix}{name}: {route} ({flags})";
                    if (matchesTarget)
                        targetRows.Add(row);
                    else if (active)
                        activeRows.Add(row);
                    else if (otherRows.Count < 2)
                        otherRows.Add(row);
                }

                var rows = targetRows
                    .Concat(activeRows)
                    .Concat(otherRows)
                    .Take(2)
                    .ToList();

                return string.Join("\n", rows);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string TryGetTailscaleServerHost()
        {
            try
            {
                var serverUrl = AppState.ServerUrl;
                if (string.IsNullOrWhiteSpace(serverUrl) || !AppState.IsTailscaleUrl(serverUrl))
                    return null;

                return Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri)
                    ? uri.Host
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool MatchesTailscaleHost(JsonArray ips, string targetHost)
        {
            if (ips == null || string.IsNullOrWhiteSpace(targetHost))
                return false;

            foreach (var ip in ips)
            {
                if (string.Equals(ip?.ToString(), targetHost, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private CancellationTokenSource _busCts;
        
        private async Task LoginAsync()
        {
            _loginButton.Opacity = 0.4f;
            _loginButton.Focusable = false;
            _statusLabel.Text = "Starting Tailscale login...\nPlease wait for login URL.";
            TailscaleDebugLog.Add("Login initiated");

            try
            {
                _busCts?.Cancel();
                _busCts = new CancellationTokenSource();

                TailscaleDebugLog.Add("Calling StartLoginInteractiveAsync...");
                await AppState.Tailscale.StartLoginInteractiveAsync();
                TailscaleDebugLog.Add("Login API call succeeded");

                var authUrl = await TryGetCurrentAuthUrlAsync();
                if (!string.IsNullOrWhiteSpace(authUrl))
                {
                    ShowAuthUrl(authUrl);
                }
                else
                {
                    _statusLabel.Text = "Waiting for login URL...";
                }

                // Start polling for authentication completion
                _ = WatchForAuthenticationCompletionAsync(_busCts.Token);
            }
            catch (Exception ex)
            {
                TailscaleDebugLog.Add($"Login failed: {ex.GetType().Name}: {ex.Message}");
                _statusLabel.Text = $"Login failed: {ex.Message}";
                _loginButton.Opacity = 1.0f;
                _loginButton.Focusable = true;
            }
        }

        private async Task WatchForAuthenticationCompletionAsync(CancellationToken token)
        {
            try
            {
                TailscaleDebugLog.Add("Starting authentication completion polling");
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(2000, token);

                    JsonNode status = null;
                    try
                    {
                        status = await AppState.Tailscale.GetStatusAsync(token);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        TailscaleDebugLog.Add($"Auth completion status check failed: {ex.GetType().Name}: {ex.Message}");
                        continue;
                    }

                    var selfNode = status?["Self"]?.AsObject();
                    var online = selfNode?["Online"]?.GetValue<bool>() ?? false;
                    var backendState = status?["BackendState"]?.ToString() ?? "Unknown";

                    if (online || backendState == "Running")
                    {
                        TailscaleDebugLog.Add("Authentication completed by status polling");
                        RunOnUiThread(() =>
                        {
                            _statusLabel.Text = "Successfully connected to tailnet!";
                            _loginLabel.Text = "Connected";
                            _loginButton.Opacity = 0.4f;
                            _loginButton.Focusable = false;
                        });
                        _busCts?.Cancel();
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                TailscaleDebugLog.Add("Authentication completion polling cancelled");
            }
        }

        private async Task<string> TryGetCurrentAuthUrlAsync()
        {
            try
            {
                var cachedAuthUrl = AppState.Tailscale?.LastAuthUrl;
                if (!string.IsNullOrWhiteSpace(cachedAuthUrl))
                    return cachedAuthUrl;

                var status = await AppState.Tailscale.GetStatusAsync();
                return ExtractAuthUrl(status);
            }
            catch (Exception ex)
            {
                TailscaleDebugLog.Add($"AuthURL status check failed: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        private async Task<bool> CheckConnectionStatusSilently()
        {
            try
            {
                if (AppState.Tailscale == null)
                    return false;

                bool reachable = AppState.Tailscale.IsRunning || AppState.Tailscale.IsSocketReachable;
                if (!reachable)
                    return false;

                var status = await AppState.Tailscale.GetStatusAsync();
                if (status == null)
                    return false;

                var backendState = status?["BackendState"]?.ToString() ?? "Unknown";
                // BackendState values: NoState, NeedsLogin, NeedsMachineAuth, Stopped,
                //                     Starting, Running
                bool daemonHealthy = backendState == "Running" || backendState == "Starting" ||
                                     backendState == "NeedsLogin" || backendState == "NeedsMachineAuth";

                if (!daemonHealthy)
                {
                    TailscaleDebugLog.Add($"Daemon unhealthy, BackendState={backendState}");
                    return false;
                }

                var selfNode = status?["Self"]?.AsObject();
                var online = selfNode?["Online"]?.GetValue<bool>() ?? false;

                // Consider connected if either online or backend is running (has valid key)
                return online || backendState == "Running";
            }
            catch (Exception ex)
            {
                TailscaleDebugLog.Add($"CheckConnectionStatusSilently error: {ex.Message}");
                return false; // Assume not connected on error
            }
        }

        private static string ExtractAuthUrl(JsonNode node)
        {
            return node?["BrowseToURL"]?.GetValue<string>()
                ?? node?["Status"]?["AuthURL"]?.GetValue<string>()
                ?? node?["AuthURL"]?.GetValue<string>();
        }

        private void ShowAuthUrl(string authUrl)
        {
            if (string.IsNullOrWhiteSpace(authUrl))
                return;

            _statusLabel.Text = $"Open this URL or scan the QR code to authenticate:\n\n{authUrl}";

            var qrPath = JellyfinTizen.Utils.QrCodeHelper.GenerateQrCode(authUrl);
            if (qrPath != null)
            {
                _qrImageView.SetImage(qrPath);
                _qrImageView.ExcludeLayouting = false;
                _qrImageView.Show();
            }
            else
            {
                _qrImageView.Hide();
                _qrImageView.ExcludeLayouting = true;
            }
        }

        private void SubscribeAuthUrlEvents()
        {
            if (AppState.Tailscale == null)
                return;

            AppState.Tailscale.AuthUrlReceived -= OnAuthUrlReceived;
            AppState.Tailscale.AuthUrlReceived += OnAuthUrlReceived;

            if (!string.IsNullOrWhiteSpace(AppState.Tailscale.LastAuthUrl))
                ShowAuthUrl(AppState.Tailscale.LastAuthUrl);
        }

        private void UnsubscribeAuthUrlEvents()
        {
            if (AppState.Tailscale != null)
                AppState.Tailscale.AuthUrlReceived -= OnAuthUrlReceived;
        }

        private void OnAuthUrlReceived(string authUrl)
        {
            RunOnUiThread(() => ShowAuthUrl(authUrl));
        }

        private void StartPeriodicRefresh()
        {
            // Cancel any existing refresh operation
            _refreshCts?.Cancel();
            _refreshCts = new CancellationTokenSource();

            // Start periodic refresh every 2 seconds
            _ = PeriodicRefreshAsync(_refreshCts.Token);
        }

        private async Task PeriodicRefreshAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(2000, token); // Check every 2 seconds
                    if (!token.IsCancellationRequested)
                    {
                        // Only check if we're not currently loading/authenticating
                        if (!_isLoading)
                        {
                            TailscaleDebugLog.Add("Periodic refresh triggered");

                            // Get current status without updating UI yet
                            bool isCurrentlyConnected = await CheckConnectionStatusSilently();

                            // Only update UI if connection state changed or we're unsure
                            if (!wasConnected && isCurrentlyConnected)
                            {
                                // Just connected - update UI to show connected state
                                TailscaleDebugLog.Add("Device just connected - updating UI");
                                wasConnected = true;
                                _ = RefreshStatusAsync(); // This will update UI
                            }
                            else if (wasConnected && !isCurrentlyConnected)
                            {
                                // Just disconnected - update UI to show disconnected state
                                TailscaleDebugLog.Add("Device just disconnected - updating UI");
                                wasConnected = false;
                                _ = RefreshStatusAsync(); // This will update UI
                            }
                            // If state hasn't changed, don't update UI to avoid flickering
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                TailscaleDebugLog.Add("Periodic refresh cancelled");
            }
            catch (Exception ex)
            {
                TailscaleDebugLog.Add($"Periodic refresh error: {ex.Message}");
            }
        }

        public void HandleKey(AppKey key)
        {
            switch (key)
            {
                case AppKey.Back:
                    NavigationService.NavigateBack();
                    break;
                case AppKey.Up:
                    if (TryScrollDebugOverlay(-1)) return;
                    break;
                case AppKey.Down:
                    if (TryScrollDebugOverlay(1)) return;
                    break;
                case AppKey.Enter:
                    if (_loginButton.Focusable && _loginButton.Opacity > 0.5f)
                        FireAndForget(LoginAsync(), nameof(LoginAsync));
                    break;
            }
        }
    }
}
