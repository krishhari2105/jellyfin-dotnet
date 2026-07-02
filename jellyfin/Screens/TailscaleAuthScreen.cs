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
using System.Linq;

namespace JellyfinTizen.Screens
{
    public class TailscaleAuthScreen : ScreenBase, IKeyHandler
    {
        private View _mainPanel;
        private TextLabel _statusLabel;
        private View _loginButton;
        private View _skipButton;
        private TextLabel _skipLabel;
        private bool _isLoading;
        private CancellationTokenSource _busCts;

        public TailscaleAuthScreen()
        {
            Initialize();
        }

        private void Initialize()
        {
            var root = UiFactory.CreateAtmosphericBackground();
            var panel = UiFactory.CreateCenteredPanel(width: 960, top: 140);
            panel.Add(UiFactory.CreateDisplayTitle("Tailscale Setup"));
            panel.Add(UiFactory.CreateSubtitle("Authenticate with your tailnet"));

            _mainPanel = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 300,
                CornerRadius = 20,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BorderlineWidth = 1.5f
            };

            _statusLabel = new TextLabel("Checking Tailscale status...")
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
            _mainPanel.Add(_statusLabel);
            panel.Add(_mainPanel);

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

            var loginLabel = new TextLabel("Log In with Tailscale")
            {
                TextColor = UiTheme.TextPrimary,
                PointSize = 28,
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _loginButton.Add(loginLabel);
            panel.Add(_loginButton);

            _skipButton = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 96,
                CornerRadius = 20,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BorderlineWidth = 1.5f,
                Focusable = true
            };
            UiFactory.SetButtonFocusState(_skipButton, focused: false);

            _skipLabel = new TextLabel("Skip for now")
            {
                TextColor = UiTheme.TextSecondary,
                PointSize = 24,
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _skipButton.Add(_skipLabel);
            panel.Add(_skipButton);

            _loginButton.FocusGained += (s, e) => UiFactory.SetButtonFocusState(_loginButton, focused: true);
            _loginButton.FocusLost += (s, e) => UiFactory.SetButtonFocusState(_loginButton, focused: false);
            _skipButton.FocusGained += (s, e) => UiFactory.SetButtonFocusState(_skipButton, focused: true);
            _skipButton.FocusLost += (s, e) => UiFactory.SetButtonFocusState(_skipButton, focused: false);

            root.Add(panel);
            Add(root);
        }

        public override void OnShow()
        {
            base.OnShow();
            ShowDebugOverlay();
            TailscaleDebugLog.Add("=== TailscaleAuthScreen shown ===");
            _loginButton.Opacity = 0.4f;
            _loginButton.Focusable = false;
            _skipButton.Opacity = 1.0f;
            _skipButton.Focusable = true;
            FocusManager.Instance.SetCurrentFocusView(_skipButton);
            _ = CheckCurrentStatusAsync();
        }

        public override void OnHide()
        {
            _busCts?.Cancel();
            base.OnHide(); // calls HideDebugOverlay()
        }

        private async Task CheckCurrentStatusAsync()
        {
            _isLoading = true;
            _statusLabel.Text = "Checking Tailscale status...";
            TailscaleDebugLog.Add("CheckCurrentStatusAsync started");

            try
            {
                if (AppState.Tailscale == null)
                {
                    TailscaleDebugLog.Add("Tailscale not available (null)");
                    _statusLabel.Text = "Tailscale is not available.\n\nYou can skip and configure it later in settings.";
                    _isLoading = false;
                    return;
                }

                bool reachable = AppState.Tailscale.IsRunning || AppState.Tailscale.IsSocketReachable;
                TailscaleDebugLog.Add($"IsRunning={AppState.Tailscale.IsRunning}, IsSocketReachable={AppState.Tailscale.IsSocketReachable}");

                if (!reachable)
                {
                    TailscaleDebugLog.Add("Tailscale daemon not reachable");
                    _statusLabel.Text = "Tailscale daemon is not running.\n\nYou can skip and configure it later.";
                    _isLoading = false;
                    return;
                }

                TailscaleDebugLog.Add("Getting status from tailscaled...");
                var status = await AppState.Tailscale.GetStatusAsync();

                var backendState = status?["BackendState"]?.ToString() ?? "Unknown";
                var online = status?["Self"]?["Online"]?.GetValue<bool>() ?? false;
                var authUrl = status?["AuthURL"]?.ToString();
                TailscaleDebugLog.Add($"BackendState={backendState}, Online={online}, AuthURL={(!string.IsNullOrEmpty(authUrl) ? "present" : "none")}");

                if (online)
                {
                    TailscaleDebugLog.Add("Already connected!");
                    _statusLabel.Text = "Tailscale is already connected!";
                    _loginButton.Opacity = 0.4f;
                    _loginButton.Focusable = false;
                    _skipLabel.Text = "Continue to Server Setup";
                    _isLoading = false;
                }
                else if (!string.IsNullOrEmpty(authUrl))
                {
                    // Auth URL already available - show it immediately
                    TailscaleDebugLog.Add($"AuthURL already available: {authUrl}");
                    _statusLabel.Text = $"Open this URL to authenticate:\n\n{authUrl}\n\nWaiting for authentication...";
                    _loginButton.Opacity = 0.4f;
                    _loginButton.Focusable = false;
                    // Watch for completion
                    _busCts?.Cancel();
                    _busCts = new CancellationTokenSource();
                    var enumerator = AppState.Tailscale.WatchIPNBus(mask: 7, _busCts.Token).GetAsyncEnumerator(_busCts.Token);
                    _ = WatchForLoginUrlWithEnumeratorAsync(enumerator, enumerator.MoveNextAsync());
                }
                else
                {
                    TailscaleDebugLog.Add($"Not connected, state={backendState}");
                    _statusLabel.Text = "Tailscale is ready but not authenticated.\n\nLog in to use your tailnet.";
                    _loginButton.Opacity = 1.0f;
                    _loginButton.Focusable = true;
                    FocusManager.Instance.SetCurrentFocusView(_loginButton);
                    _isLoading = false;
                }
            }
            catch (Exception ex)
            {
                TailscaleDebugLog.Add($"CheckCurrentStatusAsync error: {ex.GetType().Name}: {ex.Message}");
                _statusLabel.Text = "Tailscale check failed.\n\nYou can skip and configure it later in settings.";
                _isLoading = false;
            }
        }

        private async Task LoginAsync()
        {
            if (_isLoading) return;
            _isLoading = true;

            _loginButton.Opacity = 0.4f;
            _loginButton.Focusable = false;
            _statusLabel.Text = "Starting Tailscale login...\nPlease wait for login URL.";
            TailscaleDebugLog.Add("LoginAsync: started");

            try
            {
                _busCts?.Cancel();
                _busCts = new CancellationTokenSource();

                // Establish watch BEFORE calling StartLoginInteractiveAsync to avoid race condition
                TailscaleDebugLog.Add("LoginAsync: pre-establishing IPN bus watch");
                var enumerator = AppState.Tailscale.WatchIPNBus(mask: 7, _busCts.Token).GetAsyncEnumerator(_busCts.Token);
                var firstMoveTask = enumerator.MoveNextAsync();

                TailscaleDebugLog.Add("LoginAsync: calling StartLoginInteractiveAsync");
                await AppState.Tailscale.StartLoginInteractiveAsync();
                TailscaleDebugLog.Add("LoginAsync: API call succeeded, watching bus for URL");
                _statusLabel.Text = "Waiting for login URL...\n\nCheck the log below for the URL.";

                _ = WatchForLoginUrlWithEnumeratorAsync(enumerator, firstMoveTask);
            }
            catch (Exception ex)
            {
                TailscaleDebugLog.Add($"LoginAsync error: {ex.GetType().Name}: {ex.Message}");
                RunOnUiThread(() =>
                {
                    _statusLabel.Text = $"Login failed: {ex.Message}\n\nYou can skip and configure later in settings.";
                    _loginButton.Opacity = 1.0f;
                    _loginButton.Focusable = true;
                    _isLoading = false;
                });
            }
        }

        private async Task WatchForLoginUrlWithEnumeratorAsync(System.Collections.Generic.IAsyncEnumerator<JsonNode> enumerator, System.Threading.Tasks.ValueTask<bool> firstMoveTask)
        {
            try
            {
                TailscaleDebugLog.Add("WatchForLoginUrl: watching IPN bus...");
                bool hasNext = await firstMoveTask;
                while (hasNext && !_busCts.Token.IsCancellationRequested)
                {
                    var notify = enumerator.Current;
                    string json = notify?.ToJsonString() ?? "";
                    TailscaleDebugLog.Add($"Bus event: {json.Substring(0, Math.Min(150, json.Length))}");

                    // Check for login URL in BrowseToURL
                    string browseUrl = notify?["BrowseToURL"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(browseUrl))
                    {
                        TailscaleDebugLog.Add($"Got BrowseToURL: {browseUrl}");
                        RunOnUiThread(() =>
                        {
                            _statusLabel.Text = $"Open this URL to authenticate:\n\n{browseUrl}\n\nWaiting for authentication...";
                        });
                    }

                    // Also check AuthURL inside the Status sub-object
                    string authUrl = notify?["Status"]?["AuthURL"]?.GetValue<string>()
                                  ?? notify?["AuthURL"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(authUrl) && string.IsNullOrEmpty(browseUrl))
                    {
                        TailscaleDebugLog.Add($"Got AuthURL: {authUrl}");
                        RunOnUiThread(() =>
                        {
                            _statusLabel.Text = $"Open this URL to authenticate:\n\n{authUrl}\n\nWaiting for authentication...";
                        });
                    }

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
                                _loginButton.Opacity = 0.4f;
                                _loginButton.Focusable = false;
                                _skipLabel.Text = "Continue to Server Setup";
                                _skipButton.Focusable = true;
                                FocusManager.Instance.SetCurrentFocusView(_skipButton);
                            });
                            _isLoading = false;
                            _busCts.Cancel();
                            return;
                        }
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
                    _statusLabel.Text = $"Login failed: {ex.Message}\n\nYou can skip and configure later in settings.";
                    _loginButton.Opacity = 1.0f;
                    _loginButton.Focusable = true;
                    _isLoading = false;
                });
            }
            finally
            {
                await enumerator.DisposeAsync();
            }
        }

        private void SkipAuthentication()
        {
            NavigationService.Navigate(new ServerSetupScreen(), addToStack: true);
        }

        public void HandleKey(AppKey key)
        {
            switch (key)
            {
                case AppKey.Back:
                    NavigationService.NavigateBack();
                    break;

                case AppKey.Up:
                    // Login button is ABOVE skip button in layout
                    if (TryScrollDebugOverlay(-1)) return;
                    if (_loginButton.Focusable && _loginButton.Opacity > 0.5f)
                        FocusManager.Instance.SetCurrentFocusView(_loginButton);
                    break;

                case AppKey.Down:
                    // Skip button is BELOW login button in layout
                    if (TryScrollDebugOverlay(1)) return;
                    if (_skipButton.Focusable)
                        FocusManager.Instance.SetCurrentFocusView(_skipButton);
                    break;

                case AppKey.Enter:
                    if (_isLoading) return;
                    var focused = FocusManager.Instance.GetCurrentFocusView();
                    if (focused == _loginButton && _loginButton.Opacity > 0.5f)
                        FireAndForget(LoginAsync());
                    else if (focused == _skipButton)
                        SkipAuthentication();
                    break;
            }
        }
    }
}