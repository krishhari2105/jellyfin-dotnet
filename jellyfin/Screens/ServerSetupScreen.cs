using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Core;
using JellyfinTizen.UI;
using System.Threading.Tasks;
using System;
using System.Text.Json;

namespace JellyfinTizen.Screens
{
    public class ServerSetupScreen : ScreenBase, IKeyHandler
    {
        private sealed class ServerProbeResult
        {
            public string Url { get; set; }
            public string Name { get; set; }
        }

        private TextField _serverInput;
        private View _continueButton;
        private TextLabel _continueText;
        private TextLabel _errorLabel;

        public ServerSetupScreen()
        {
            var root = MonochromeAuthFactory.CreateBackground();
            var panel = MonochromeAuthFactory.CreatePanel();
            panel.Add(MonochromeAuthFactory.CreateTitle("Connect To Your Jellyfin Server"));
            panel.Add(MonochromeAuthFactory.CreateSubtitle("Enter your server URL to continue."));

            var serverInputShell = MonochromeAuthFactory.CreateInputFieldShell("http://192.168.1.10:8096", out _serverInput);
            _continueButton = MonochromeAuthFactory.CreateButton("Continue", out _continueText, primary: true);
            _errorLabel = MonochromeAuthFactory.CreateErrorLabel();

            panel.Add(serverInputShell);
            panel.Add(_continueButton);
            panel.Add(_errorLabel);
            root.Add(panel);
            Add(root);
        }

        public override void OnShow()
        {
            if (string.IsNullOrWhiteSpace(_serverInput.Text))
            {
                var saved = AppState.ServerUrl;
                if (!string.IsNullOrWhiteSpace(saved))
                {
                    _serverInput.Text = saved;
                }
            }
            FocusManager.Instance.SetCurrentFocusView(_serverInput);
            HighlightButton(false);
        }

        public void HandleKey(AppKey key)
        {
            var focused = FocusManager.Instance.GetCurrentFocusView();

            switch (key)
            {
                case AppKey.Back:
                    NavigationService.NavigateBack();
                    return;

                case AppKey.Down:
                    if (focused == _serverInput)
                    {
                        FocusManager.Instance.SetCurrentFocusView(_continueButton);
                        HighlightButton(true);
                        return;
                    }
                    break;

                case AppKey.Up:
                    if (focused == _continueButton)
                    {
                        HighlightButton(false);
                        FocusManager.Instance.SetCurrentFocusView(_serverInput);
                        return;
                    }
                    break;

                case AppKey.Enter:
                    if (focused == _continueButton)
                    {
                        _ = OnContinueAsync();
                        return;
                    }
                    break;
            }
        }

        private void HighlightButton(bool focused)
        {
            MonochromeAuthFactory.SetButtonFocusState(_continueButton, primary: true, focused: focused);
        }

        private async Task OnContinueAsync()
        {
            var url = _serverInput.Text?.Trim();
            if (string.IsNullOrEmpty(url))
                return;

            // Auto prepend http or https if not already present
            var probeResult = await ValidateAndPrependProtocol(url);
            if (probeResult == null || string.IsNullOrWhiteSpace(probeResult.Url))
            {
                ShowErrorMessage("Server not found. Please try again.");
                return;
            }

            if (!AppState.CanStoreServer(probeResult.Url))
            {
                ShowErrorMessage($"You can save up to {AppState.MaxStoredServers} servers.");
                return;
            }

            if (!AppState.TrySaveServer(probeResult.Url, probeResult.Name))
            {
                ShowErrorMessage("Unable to save server. Please try again.");
                return;
            }

            RunOnUiThread(() =>
            {
                NavigationService.Navigate(new LoadingScreen("Fetching users..."));
            });

            try
            {
                var users = await AppState.Jellyfin.GetPublicUsersAsync();
                RunOnUiThread(() =>
                {
                    NavigationService.Navigate(
                        new UserSelectScreen(users),
                        addToStack: true
                    );
                });
            }
            catch
            {
                RunOnUiThread(() =>
                {
                    NavigationService.NavigateBack();
                    ShowErrorMessage("Failed to connect. Please try again.");
                });
            }
        }

        private async Task<ServerProbeResult> ValidateAndPrependProtocol(string url)
        {
            // If already has http or https, use as is
            if (url.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase))
            {
                return await ResolveServerBaseUrl(url);
            }

            // Prefer https first so we do not persist an http endpoint that only works via redirect.
            string httpsUrl = "https://" + url;
            var resolvedHttps = await ResolveServerBaseUrl(httpsUrl);
            if (resolvedHttps != null && !string.IsNullOrWhiteSpace(resolvedHttps.Url))
            {
                return resolvedHttps;
            }

            // Fallback to http only when https is unavailable.
            string httpUrl = "http://" + url;
            var resolvedHttp = await ResolveServerBaseUrl(httpUrl);
            if (resolvedHttp != null && !string.IsNullOrWhiteSpace(resolvedHttp.Url))
            {
                return resolvedHttp;
            }

            return null;
        }

        private async Task<ServerProbeResult> ResolveServerBaseUrl(string url)
        {
            try
            {
                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.Timeout = System.TimeSpan.FromSeconds(5);
                var normalizedInput = url.TrimEnd('/');
                var response = await httpClient.GetAsync(normalizedInput + "/System/Info/Public");
                if (!response.IsSuccessStatusCode)
                    return null;

                var payload = await response.Content.ReadAsStringAsync();
                string serverName = null;
                if (!string.IsNullOrWhiteSpace(payload))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(payload);
                        if (doc.RootElement.TryGetProperty("ServerName", out var nameProp))
                            serverName = nameProp.GetString();
                    }
                    catch
                    {
                    }
                }

                // Capture the final URL after redirects (http->https, reverse proxy path rewrites, etc).
                var finalUri = response.RequestMessage?.RequestUri;
                if (finalUri == null)
                {
                    return new ServerProbeResult
                    {
                        Url = normalizedInput,
                        Name = serverName
                    };
                }

                var absolute = finalUri.GetLeftPart(System.UriPartial.Authority) + finalUri.AbsolutePath;
                const string infoPublicSuffix = "/System/Info/Public";
                if (absolute.EndsWith(infoPublicSuffix, System.StringComparison.OrdinalIgnoreCase))
                    absolute = absolute.Substring(0, absolute.Length - infoPublicSuffix.Length);

                return new ServerProbeResult
                {
                    Url = absolute.TrimEnd('/'),
                    Name = serverName
                };
            }
            catch
            {
                return null;
            }
        }

        private void ShowErrorMessage(string message)
        {
            _errorLabel.Text = message;
            
            // Clear error after 5 seconds
            var timer = new System.Timers.Timer(5000);
            timer.Elapsed += (sender, e) =>
            {
                RunOnUiThread(() => _errorLabel.Text = string.Empty);
                timer.Stop();
                timer.Dispose();
            };
            timer.Start();
        }
    }
}
