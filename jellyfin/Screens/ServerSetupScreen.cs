using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Core;
using JellyfinTizen.UI;
using System.Threading.Tasks;
using System;
using System.Text.Json;
using System.Net.Http;

namespace JellyfinTizen.Screens
{
    public class ServerSetupScreen : ScreenBase, IKeyHandler
    {
        private sealed class ServerProbeResult
        {
            public string Url { get; set; }
            public string Name { get; set; }
            public bool IsEmby { get; set; }
        }

        private TextField _serverInput;
        private View _continueButton;
        private View _tailscaleButton;
        private TextLabel _errorLabel;
        private bool _continueInProgress;
        private System.Threading.Timer _errorTimer;
        private HttpClient _probeHttpClient;

        public ServerSetupScreen()
        {
            var root = MonochromeAuthFactory.CreateBackground();
            var panel = MonochromeAuthFactory.CreatePanel();
            panel.Add(MonochromeAuthFactory.CreateTitle("Connect To Your Jellyfin Server"));
            panel.Add(MonochromeAuthFactory.CreateSubtitle("Enter your server URL to continue."));

            var serverInputShell = MonochromeAuthFactory.CreateInputFieldShell("http://192.168.1.10:8096", out _serverInput);
            _continueButton = MonochromeAuthFactory.CreateButton("Continue", out _);
            if (AppState.Tailscale != null)
            {
                _tailscaleButton = MonochromeAuthFactory.CreateButton("Tailscale Settings", out _);
            }
            _errorLabel = MonochromeAuthFactory.CreateErrorLabel();

            panel.Add(serverInputShell);
            panel.Add(_continueButton);
            if (_tailscaleButton != null)
            {
                panel.Add(_tailscaleButton);
            }
            panel.Add(_errorLabel);
            root.Add(panel);
            Add(root);
        }

        public override void OnShow()
        {
            _continueInProgress = false;
            if (string.IsNullOrWhiteSpace(_serverInput.Text))
            {
                var saved = AppState.ServerUrl;
                if (!string.IsNullOrWhiteSpace(saved))
                {
                    _serverInput.Text = saved;
                }
            }
            FocusManager.Instance.SetCurrentFocusView(_serverInput);
            HighlightButton(_continueButton, false);
            HighlightButton(_tailscaleButton, false);
            ShowDebugOverlay();
            Core.TailscaleDebugLog.Add("=== ServerSetupScreen shown ===");
        }

        public override void OnHide()
        {
            _continueInProgress = false;
            DisposeTimer(ref _errorTimer);
            _probeHttpClient?.Dispose();
            _probeHttpClient = null;
            base.OnHide(); // calls HideDebugOverlay()
        }

        public void HandleKey(AppKey key)
        {
            if (_continueInProgress)
                return;

            var focused = FocusManager.Instance.GetCurrentFocusView();

            switch (key)
            {
                case AppKey.Back:
                    NavigationService.NavigateBack();
                    return;

                case AppKey.Up:
                    if (TryScrollDebugOverlay(-1)) return;
                    if (focused == _continueButton)
                    {
                        HighlightButton(_continueButton, false);
                        FocusManager.Instance.SetCurrentFocusView(_serverInput);
                        return;
                    }
                    else if (focused == _tailscaleButton)
                    {
                        HighlightButton(_tailscaleButton, false);
                        FocusManager.Instance.SetCurrentFocusView(_continueButton);
                        HighlightButton(_continueButton, true);
                        return;
                    }
                    break;

                case AppKey.Down:
                    if (TryScrollDebugOverlay(1)) return;
                    if (focused == _serverInput)
                    {
                        FocusManager.Instance.SetCurrentFocusView(_continueButton);
                        HighlightButton(_continueButton, true);
                        HighlightButton(_tailscaleButton, false);
                        return;
                    }
                    else if (focused == _continueButton && _tailscaleButton != null)
                    {
                        FocusManager.Instance.SetCurrentFocusView(_tailscaleButton);
                        HighlightButton(_continueButton, false);
                        HighlightButton(_tailscaleButton, true);
                        return;
                    }
                    break;

                case AppKey.Enter:
                    if (focused == _continueButton)
                    {
                        FireAndForget(OnContinueAsync());
                        return;
                    }
                    else if (focused == _tailscaleButton)
                    {
                        NavigationService.Navigate(new TailscaleScreen());
                        return;
                    }
                    break;
            }
        }

        private void HighlightButton(View button, bool focused)
        {
            if (button != null)
            {
                MonochromeAuthFactory.SetButtonFocusState(button, focused: focused);
            }
        }

        private async Task OnContinueAsync()
        {
            if (_continueInProgress)
                return;

            var url = _serverInput.Text?.Trim();
            if (string.IsNullOrEmpty(url))
            {
                ShowErrorMessage("Server URL required.");
                return;
            }

            _continueInProgress = true;
            DisposeTimer(ref _errorTimer);
            _errorLabel.Text = string.Empty;
            Core.TailscaleDebugLog.Add($"Attempting to connect to: {url}");
            Core.TailscaleDebugLog.Add($"Tailscale IsRunning={AppState.Tailscale?.IsRunning}, IsSocketReachable={AppState.Tailscale?.IsSocketReachable}");

            try
            {
                // Auto prepend http or https if not already present
                var probeResult = await ValidateAndPrependProtocol(url);
                if (probeResult == null || string.IsNullOrWhiteSpace(probeResult.Url))
                {
                    Core.TailscaleDebugLog.Add("Server probe returned null - server not found");
                    ShowErrorMessage("Server not found. Please try again.");
                    return;
                }

                Core.TailscaleDebugLog.Add($"Server found: {probeResult.Url} ({probeResult.Name})");

                if (!AppState.CanStoreServer(probeResult.Url))
                {
                    ShowErrorMessage($"You can save up to {AppState.MaxStoredServers} servers.");
                    return;
                }

                if (!AppState.TrySaveServer(probeResult.Url, probeResult.Name, probeResult.IsEmby))
                {
                    ShowErrorMessage("Unable to save server. Please try again.");
                    return;
                }

                RunOnUiThread(() =>
                {
                    NavigationService.Navigate(new LoadingScreen("Fetching users..."));
                });

                var users = await AppState.Jellyfin.GetPublicUsersAsync();
                RunOnUiThread(() =>
                {
                    NavigationService.Navigate(
                        new UserSelectScreen(users),
                        addToStack: true
                    );
                });
            }
            catch (Exception ex)
            {
                Core.TailscaleDebugLog.Add($"OnContinueAsync error: {ex.GetType().Name}: {ex.Message}");
                RunOnUiThread(() =>
                {
                    NavigationService.NavigateBack();
                    ShowErrorMessage("Failed to connect. Please try again.");
                });
            }
            finally
            {
                _continueInProgress = false;
            }
        }

        private async Task<ServerProbeResult> ValidateAndPrependProtocol(string url)
        {
            // If already has http or https, use as is
            if (url.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase))
            {
                Core.TailscaleDebugLog.Add($"URL already has scheme, probing: {url}");
                return await ResolveServerBaseUrl(url);
            }

            // Prefer https first so we do not persist an http endpoint that only works via redirect.
            string httpsUrl = "https://" + url;
            Core.TailscaleDebugLog.Add($"Probing HTTPS: {httpsUrl}");
            var resolvedHttps = await ResolveServerBaseUrl(httpsUrl);
            if (resolvedHttps != null && !string.IsNullOrWhiteSpace(resolvedHttps.Url))
                return resolvedHttps;

            // Fallback to http only when https is unavailable.
            string httpUrl = "http://" + url;
            Core.TailscaleDebugLog.Add($"Probing HTTP: {httpUrl}");
            var resolvedHttp = await ResolveServerBaseUrl(httpUrl);
            if (resolvedHttp != null && !string.IsNullOrWhiteSpace(resolvedHttp.Url))
                return resolvedHttp;

            return null;
        }

        private async Task<ServerProbeResult> ResolveServerBaseUrl(string url)
        {
            try
            {
                Core.TailscaleDebugLog.Add($"ResolveServerBaseUrl: {url}");
                _probeHttpClient ??= new HttpClient(new HttpClientHandler
                {
                    Proxy = new TailscaleWebProxy(),
                    UseProxy = true,
                    AllowAutoRedirect = true,
                })
                {
                    Timeout = TimeSpan.FromSeconds(8)
                };
                var httpClient = _probeHttpClient;
                var normalizedInput = url.TrimEnd('/');
                var targetUrl = normalizedInput + "/System/Info/Public";
                Core.TailscaleDebugLog.Add($"GET {targetUrl}");
                using var response = await httpClient.GetAsync(targetUrl);
                Core.TailscaleDebugLog.Add($"Response: {(int)response.StatusCode} {response.StatusCode}");
                if (!response.IsSuccessStatusCode)
                    return null;

                var payload = await response.Content.ReadAsStringAsync();
                string serverName = null;
                bool isEmby = false;
                if (!string.IsNullOrWhiteSpace(payload))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(payload);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("ServerName", out var nameProp))
                            serverName = nameProp.GetString();

                        if (root.TryGetProperty("ProductName", out var prodProp))
                        {
                            var prodName = prodProp.GetString();
                            if (prodName != null && prodName.IndexOf("emby", StringComparison.OrdinalIgnoreCase) >= 0)
                                isEmby = true;
                        }
                        else
                        {
                            isEmby = true;
                        }
                        Core.TailscaleDebugLog.Add($"Server name: {serverName}, IsEmby: {isEmby}");
                    }
                    catch (Exception ex)
                    {
                        Core.TailscaleDebugLog.Add($"JSON parse error: {ex.Message}");
                    }
                }

                var finalUri = response.RequestMessage?.RequestUri;
                if (finalUri == null)
                {
                    return new ServerProbeResult { Url = normalizedInput, Name = serverName, IsEmby = isEmby };
                }

                var absolute = finalUri.GetLeftPart(System.UriPartial.Authority) + finalUri.AbsolutePath;
                const string infoPublicSuffix = "/System/Info/Public";
                if (absolute.EndsWith(infoPublicSuffix, System.StringComparison.OrdinalIgnoreCase))
                    absolute = absolute.Substring(0, absolute.Length - infoPublicSuffix.Length);

                Core.TailscaleDebugLog.Add($"Resolved server URL: {absolute.TrimEnd('/')}");
                return new ServerProbeResult { Url = absolute.TrimEnd('/'), Name = serverName, IsEmby = isEmby };
            }
            catch (Exception ex)
            {
                Core.TailscaleDebugLog.Add($"ResolveServerBaseUrl error: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        private void ShowErrorMessage(string message)
        {
            ShowTransientMessage(_errorLabel, message, ref _errorTimer);
        }
    }
}
