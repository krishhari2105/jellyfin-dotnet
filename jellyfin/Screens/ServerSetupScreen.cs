using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Core;
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
            var container = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    CellPadding = new Size2D(0, 40)
                }
            };

            var title = new TextLabel("Enter Jellyfin Server URL")
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                PointSize = 52,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            _serverInput = new TextField
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 90,
                PlaceholderText = "http://192.168.1.10:8096",
                PointSize = 36,
                BackgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f),
                TextColor = Color.White,
                Focusable = true
            };

            // "Button" as a View
            _continueButton = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 90,
                BackgroundColor = new Color(0.25f, 0.25f, 0.25f, 1f),
                Focusable = true
            };

            _continueText = new TextLabel("Continue")
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                PointSize = 40,
                TextColor = Color.White
            };

            _continueButton.Add(_continueText);

            // Error label
            _errorLabel = new TextLabel(string.Empty)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                PointSize = 32,
                TextColor = Color.Red,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            container.Add(title);
            container.Add(_serverInput);
            container.Add(_continueButton);
            container.Add(_errorLabel);
            Add(container);
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
            _continueButton.BackgroundColor =
                focused ? new Color(0.35f, 0.35f, 0.35f, 1f)
                        : new Color(0.25f, 0.25f, 0.25f, 1f);
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
                return url;
            }

            // Try with http first
            string httpUrl = "http://" + url;
            if (await IsServerReachable(httpUrl))
            {
                return httpUrl;
            }

            // Try with https if http failed
            string httpsUrl = "https://" + url;
            if (await IsServerReachable(httpsUrl))
            {
                return httpsUrl;
            }

            return null;
        }

        private async Task<bool> IsServerReachable(string url)
        {
            try
            {
                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.Timeout = System.TimeSpan.FromSeconds(5);
                var response = await httpClient.GetAsync(url + "/System/Info/Public");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
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
