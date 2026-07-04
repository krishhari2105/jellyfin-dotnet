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
        private ImageView _qrImageView;
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
            var panel = UiFactory.CreateCenteredPanel(width: 1000, top: 50);
            panel.Add(UiFactory.CreateDisplayTitle("Tailscale Setup"));
            panel.Add(UiFactory.CreateSubtitle("Authenticate with your tailnet"));

            _mainPanel = new View
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

            _statusLabel = new TextLabel("Checking Tailscale status...")
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
            _mainPanel.Add(_statusLabel);

            _qrImageView = new ImageView
            {
                WidthSpecification = 320,
                HeightSpecification = 320,
                ExcludeLayouting = true
            };
            _qrImageView.Hide();
            _mainPanel.Add(_qrImageView);

            panel.Add(_mainPanel);

            _loginButton = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 88,
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
                HeightSpecification = 88,
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
            SubscribeAuthUrlEvents();
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
            UnsubscribeAuthUrlEvents();
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
                var authUrl = AppState.Tailscale?.LastAuthUrl;
                if (string.IsNullOrWhiteSpace(authUrl))
                    authUrl = ExtractAuthUrl(status);
                TailscaleDebugLog.Add($"BackendState={backendState}, Online={online}, AuthURL={(!string.IsNullOrEmpty(authUrl) ? "present" : "none")}");

                if (online)
                {
                    TailscaleDebugLog.Add("Already connected!");
                    AppState.Tailscale?.ClearAuthUrl();
                    _statusLabel.Text = "Tailscale is already connected!";
                    _loginButton.Opacity = 0.4f;
                    _loginButton.Focusable = false;
                    _skipLabel.Text = "Continue to Server Setup";
                    _isLoading = false;
                    _qrImageView.Hide();
                    _qrImageView.ExcludeLayouting = true;
                }
                else if (!string.IsNullOrEmpty(authUrl))
                {
                    // Auth URL already available - show it immediately
                    TailscaleDebugLog.Add($"AuthURL already available: {authUrl}");
                    ShowAuthUrl(authUrl);
                }
                else
                {
                    TailscaleDebugLog.Add($"Not connected, state={backendState}");
                    _statusLabel.Text = "Tailscale is ready but not authenticated.\n\nLog in to use your tailnet.";
                    _loginButton.Opacity = 1.0f;
                    _loginButton.Focusable = true;
                    FocusManager.Instance.SetCurrentFocusView(_loginButton);
                    _isLoading = false;
                    _qrImageView.Hide();
                    _qrImageView.ExcludeLayouting = true;
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

                TailscaleDebugLog.Add("LoginAsync: calling StartLoginInteractiveAsync");
                await AppState.Tailscale.StartLoginInteractiveAsync();
                TailscaleDebugLog.Add("LoginAsync: API call succeeded, checking for auth URL");

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

        // Removed WatchForLoginUrlWithEnumeratorAsync - using polling approach instead
        // which is more reliable for detecting authentication completion

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

                    var authUrl = ExtractAuthUrl(status);
                    if (!string.IsNullOrWhiteSpace(authUrl))
                        RunOnUiThread(() => ShowAuthUrl(authUrl));

                    // Enhanced connection detection - check multiple indicators
                    var selfNode = status?["Self"]?.AsObject();
                    var online = selfNode?["Online"]?.GetValue<bool>() ?? false;
                    var backendState = status?["BackendState"]?.ToString() ?? "Unknown";
                    var running = status?["Running"]?.GetValue<bool>() ?? false;

                    bool isConnected = online || backendState == "Running" || running;

                    if (isConnected)
                    {
                        TailscaleDebugLog.Add($"Authentication completed by status polling: online={online}, backendState={backendState}, running={running}");
                        RunOnUiThread(ShowConnected);
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

        private static string ExtractAuthUrl(JsonNode node)
        {
            return node?["BrowseToURL"]?.ToString()
                ?? node?["Status"]?["AuthURL"]?.ToString()
                ?? node?["AuthURL"]?.ToString();
        }

        private static bool IsConnectedStatus(JsonNode node)
        {
            if (node == null)
                return false;

            if (node["Self"]?["Online"]?.GetValue<bool>() == true)
                return true;

            if (node["Status"]?["Self"]?["Online"]?.GetValue<bool>() == true)
                return true;

            var backendState = node["BackendState"]?.ToString()
                ?? node["Status"]?["BackendState"]?.ToString();

            return string.Equals(backendState, "Running", StringComparison.OrdinalIgnoreCase);
        }

        private void ShowAuthUrl(string authUrl)
        {
            if (string.IsNullOrWhiteSpace(authUrl))
                return;

            _statusLabel.Text = $"Open this URL or scan the QR code to authenticate:\n\n{authUrl}";
            _loginButton.Opacity = 0.4f;
            _loginButton.Focusable = false;
            _skipButton.Opacity = 1.0f;
            _skipButton.Focusable = true;
            _isLoading = false;

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

        private void ShowConnected()
        {
            // Clear the cached auth URL so re-entering this screen won't flash
            // the stale QR code before the status check runs.
            AppState.Tailscale?.ClearAuthUrl();

            _statusLabel.Text = "Successfully connected to tailnet!";
            _loginButton.Opacity = 0.4f;
            _loginButton.Focusable = false;
            _skipLabel.Text = "Continue to Server Setup";
            _skipButton.Opacity = 1.0f;
            _skipButton.Focusable = true;
            _isLoading = false;
            FocusManager.Instance.SetCurrentFocusView(_skipButton);
            _qrImageView.Hide();
            _qrImageView.ExcludeLayouting = true;
        }

        private void SubscribeAuthUrlEvents()
        {
            if (AppState.Tailscale == null)
                return;

            AppState.Tailscale.AuthUrlReceived -= OnAuthUrlReceived;
            AppState.Tailscale.AuthUrlReceived += OnAuthUrlReceived;

            // Do NOT eagerly call ShowAuthUrl here — CheckCurrentStatusAsync (called
            // right after) reads LastAuthUrl itself AND first verifies we are not
            // already connected. Calling ShowAuthUrl here races with that check and
            // causes the QR code to persist even after a successful authentication.
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
                    var focused = FocusManager.Instance.GetCurrentFocusView();
                    if (focused == _skipButton)
                        SkipAuthentication();
                    else if (!_isLoading && focused == _loginButton && _loginButton.Opacity > 0.5f)
                        FireAndForget(LoginAsync());
                    break;
            }
        }
    }
}
