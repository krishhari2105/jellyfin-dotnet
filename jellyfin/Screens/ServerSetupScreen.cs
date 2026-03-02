using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Core;
using JellyfinTizen.UI;
using System.Threading.Tasks;
using System;

namespace JellyfinTizen.Screens
{
    public class ServerSetupScreen : ScreenBase, IKeyHandler
    {
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
                        OnContinue();
                        return;
                    }
                    break;
            }
        }

        private void HighlightButton(bool focused)
        {
            MonochromeAuthFactory.SetButtonFocusState(_continueButton, primary: true, focused: focused);
        }

        private async void OnContinue()
        {
            var url = _serverInput.Text?.Trim();
            if (string.IsNullOrEmpty(url))
                return;

            // Auto prepend http or https if not already present
            string validatedUrl = await ValidateAndPrependProtocol(url);
            if (string.IsNullOrEmpty(validatedUrl))
            {
                ShowErrorMessage("Server not found. Please try again.");
                return;
            }

            // Reset stale runtime/session auth before starting a new server login flow.
            AppState.ClearSession(clearServer: false);
            AppState.SaveServer(validatedUrl);
            AppState.Jellyfin.Connect(validatedUrl);
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

        private async Task<string> ValidateAndPrependProtocol(string url)
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
            if (!string.IsNullOrEmpty(resolvedHttps))
            {
                return resolvedHttps;
            }

            // Fallback to http only when https is unavailable.
            string httpUrl = "http://" + url;
            var resolvedHttp = await ResolveServerBaseUrl(httpUrl);
            if (!string.IsNullOrEmpty(resolvedHttp))
            {
                return resolvedHttp;
            }

            return null;
        }

        private async Task<string> ResolveServerBaseUrl(string url)
        {
            try
            {
                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.Timeout = System.TimeSpan.FromSeconds(5);
                var normalizedInput = url.TrimEnd('/');
                var response = await httpClient.GetAsync(normalizedInput + "/System/Info/Public");
                if (!response.IsSuccessStatusCode)
                    return null;

                // Capture the final URL after redirects (http->https, reverse proxy path rewrites, etc).
                var finalUri = response.RequestMessage?.RequestUri;
                if (finalUri == null)
                    return normalizedInput;

                var absolute = finalUri.GetLeftPart(System.UriPartial.Authority) + finalUri.AbsolutePath;
                const string infoPublicSuffix = "/System/Info/Public";
                if (absolute.EndsWith(infoPublicSuffix, System.StringComparison.OrdinalIgnoreCase))
                    absolute = absolute.Substring(0, absolute.Length - infoPublicSuffix.Length);

                return absolute.TrimEnd('/');
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
