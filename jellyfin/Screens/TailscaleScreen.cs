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
        private View _loginButton;
        private TextLabel _loginLabel;
        private bool _isLoading;

        public TailscaleScreen()
        {
            Initialize();
        }

        private void Initialize()
        {
            var root = UiFactory.CreateAtmosphericBackground();
            var panel = UiFactory.CreateCenteredPanel(width: 960, top: 140);
            panel.Add(UiFactory.CreateDisplayTitle("Tailscale"));
            panel.Add(UiFactory.CreateSubtitle("Tailnet connectivity"));

            _statusPanel = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 200,
                CornerRadius = 20,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BorderlineWidth = 1.5f
            };

            _statusLabel = new TextLabel("Checking status...")
            {
                TextColor = UiTheme.TextPrimary,
                PointSize = 26,
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word
            };
            _statusPanel.Add(_statusLabel);
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
            TailscaleDebugLog.Add("=== TailscaleScreen shown ===");
            _ = RefreshStatusAsync();
            
            // Set initial focus to login button if it's focusable
            if (_loginButton.Focusable)
            {
                FocusManager.Instance.SetCurrentFocusView(_loginButton);
            }
        }

        public override void OnHide()
        {
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

                var authUrl = status?["AuthURL"]?.ToString();
                string statusText;
                if (online)
                {
                    statusText = $"Connected to tailnet\n\nHostname: {hostname}\nIPs: {ipList}";
                    TailscaleDebugLog.Add("Status: Connected to tailnet");
                    _loginLabel.Text = "Connected";
                    _loginButton.Opacity = 0.4f;
                    _loginButton.Focusable = false;
                }
                else if (backendState == "NeedsLogin" || !string.IsNullOrEmpty(authUrl))
                {
                    _loginLabel.Text = "Log In with Tailscale";
                    _loginButton.Opacity = 1.0f;
                    _loginButton.Focusable = true;
                    if (!string.IsNullOrEmpty(authUrl))
                    {
                        statusText = $"Open this URL to authenticate:\n\n{authUrl}";
                        TailscaleDebugLog.Add($"Status: Waiting for login, URL={authUrl}");
                    }
                    else
                    {
                        statusText = $"Not logged in.\nPress 'Log In with Tailscale' to authenticate.";
                        TailscaleDebugLog.Add("Status: NeedsLogin, no authUrl yet");
                    }
                }
                else
                {
                    _loginLabel.Text = "Log In with Tailscale";
                    _loginButton.Opacity = 1.0f;
                    _loginButton.Focusable = true;
                    statusText = $"Tailscale state: {backendState}\nHostname: {hostname}";
                    TailscaleDebugLog.Add($"Status: State={backendState}, not online");
                }

                _statusLabel.Text = statusText;
            }
            catch (Exception ex)
            {
                TailscaleDebugLog.Add($"ERROR: {ex.GetType().Name}: {ex.Message}");
                _statusLabel.Text = $"Error: {ex.Message}";
                _loginButton.Opacity = 0.4f;
                _loginButton.Focusable = false;
            }
            finally
            {
                _isLoading = false;
                TailscaleDebugLog.Add("RefreshStatusAsync completed");
            }
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

                // 1. Establish watch BEFORE calling StartLoginInteractiveAsync to avoid race conditions
                TailscaleDebugLog.Add("Pre-establishing IPN bus watch...");
                var enumerator = AppState.Tailscale.WatchIPNBus(mask: 7, _busCts.Token).GetAsyncEnumerator(_busCts.Token);
                var firstMoveTask = enumerator.MoveNextAsync();

                // 2. Call login-interactive
                TailscaleDebugLog.Add("Calling StartLoginInteractiveAsync...");
                await AppState.Tailscale.StartLoginInteractiveAsync();
                TailscaleDebugLog.Add("Login API call succeeded");
                
                _statusLabel.Text = "Waiting for login URL...";
                _ = WatchForLoginUrlWithEnumeratorAsync(enumerator, firstMoveTask);
            }
            catch (Exception ex)
            {
                TailscaleDebugLog.Add($"Login failed: {ex.GetType().Name}: {ex.Message}");
                _statusLabel.Text = $"Login failed: {ex.Message}";
                _loginButton.Opacity = 1.0f;
                _loginButton.Focusable = true;
            }
        }

        private async Task WatchForLoginUrlWithEnumeratorAsync(System.Collections.Generic.IAsyncEnumerator<JsonNode> enumerator, System.Threading.Tasks.ValueTask<bool> firstMoveTask)
        {
            try
            {
                TailscaleDebugLog.Add("Watching IPN bus for events...");
                
                bool hasNext = await firstMoveTask;
                while (hasNext && !_busCts.Token.IsCancellationRequested)
                {
                    var notify = enumerator.Current;
                    string json = notify?.ToJsonString() ?? "";
                    TailscaleDebugLog.Add($"Bus event: {json.Substring(0, Math.Min(120, json.Length))}");
                    
                    // Check for login URL
                    string browseUrl = notify?["BrowseToURL"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(browseUrl))
                    {
                        TailscaleDebugLog.Add($"Got BrowseToURL: {browseUrl}");
                        RunOnUiThread(() =>
                        {
                            _statusLabel.Text = $"Open this URL to authenticate:\n\n{browseUrl}";
                        });
                    }
                    
                    // Check if online
                    var self = notify?["Self"];
                    if (self is JsonNode selfNode)
                    {
                        bool online = selfNode["Online"]?.GetValue<bool>() ?? false;
                        if (online)
                        {
                            TailscaleDebugLog.Add("Authentication successful!");
                            RunOnUiThread(() =>
                            {
                                _statusLabel.Text = "Successfully connected to tailnet!";
                                _loginLabel.Text = "Connected";
                                _loginButton.Opacity = 0.4f;
                                _loginButton.Focusable = false;
                            });
                            _busCts.Cancel();
                            return;
                        }
                    }
                    
                    // Check state changes
                    var stateNode = notify?["State"];
                    if (stateNode is JsonNode state && state.GetValue<int>() == 6) // Running
                    {
                        TailscaleDebugLog.Add("State changed to Running");
                        RunOnUiThread(() =>
                        {
                            _statusLabel.Text = "Connected to tailnet!";
                            _loginLabel.Text = "Connected";
                            _loginButton.Opacity = 0.4f;
                            _loginButton.Focusable = false;
                        });
                        _busCts.Cancel();
                        return;
                    }

                    hasNext = await enumerator.MoveNextAsync();
                }
            }
            catch (OperationCanceledException)
            {
                TailscaleDebugLog.Add("Bus watch cancelled");
            }
            catch (Exception ex)
            {
                TailscaleDebugLog.Add($"Bus watch error: {ex.GetType().Name}: {ex.Message}");
                RunOnUiThread(() =>
                {
                    _statusLabel.Text = $"Error watching login: {ex.Message}";
                    _loginButton.Opacity = 1.0f;
                    _loginButton.Focusable = true;
                });
            }
            finally
            {
                await enumerator.DisposeAsync();
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
                        FireAndForget(LoginAsync());
                    break;
            }
        }
    }
}