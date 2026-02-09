using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Core;
using System;

namespace JellyfinTizen.Screens
{
    public class PasswordScreen : ScreenBase, IKeyHandler
    {
        private TextField _passwordInput;
        private View _loginButton;
        private TextLabel _loginText;
        private bool _loginFocused;
        private TextLabel _errorLabel;

        public PasswordScreen(string username)
        {
            AppState.Username = username;

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

            var title = new TextLabel($"Login as {username}")
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                PointSize = 52,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            _passwordInput = new TextField
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 90,
                PlaceholderText = "Password",
                PointSize = 36,
                BackgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f),
                TextColor = Color.White,
                Focusable = true
            };

            var hiddenInput = new PropertyMap();
            hiddenInput.Add(HiddenInputProperty.Mode, new PropertyValue((int)HiddenInputModeType.ShowLastCharacter));
            hiddenInput.Add(HiddenInputProperty.ShowLastCharacterDuration, new PropertyValue(500));
            hiddenInput.Add(HiddenInputProperty.SubstituteCharacter, new PropertyValue(0x2A));
            _passwordInput.HiddenInputSettings = hiddenInput;

            _loginButton = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 90,
                BackgroundColor = new Color(0.25f, 0.25f, 0.25f, 1f),
                Focusable = true
            };

            _loginText = new TextLabel("Login")
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                PointSize = 40,
                TextColor = Color.White
            };

            _loginButton.Add(_loginText);

            // Error label
            _errorLabel = new TextLabel(string.Empty)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                PointSize = 32,
                TextColor = Color.Red,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            container.Add(title);
            container.Add(_passwordInput);
            container.Add(_loginButton);
            container.Add(_errorLabel);
            Add(container);
        }

        public override void OnShow()
        {
            FocusManager.Instance.SetCurrentFocusView(_passwordInput);
            _loginFocused = false;
        }

        public void HandleKey(AppKey key)
        {
            switch (key)
            {
                case AppKey.Back:
                    NavigationService.NavigateBack();
                    return;

                case AppKey.Down:
                    FocusLogin(true);
                    break;

                case AppKey.Up:
                    FocusLogin(false);
                    break;

                case AppKey.Enter:
                    if (_loginFocused)
                        Login();
                    break;
            }
        }

        private void FocusLogin(bool focused)
        {
            _loginFocused = focused;

            if (focused)
            {
                FocusManager.Instance.SetCurrentFocusView(_loginButton);
                _loginButton.BackgroundColor = new Color(0.35f, 0.35f, 0.35f, 1f);
            }
            else
            {
                FocusManager.Instance.SetCurrentFocusView(_passwordInput);
                _loginButton.BackgroundColor = new Color(0.25f, 0.25f, 0.25f, 1f);
            }
        }

        private async void Login()
        {
            var password = _passwordInput.Text;

            if (string.IsNullOrEmpty(password))
                return;

            NavigationService.Navigate(
                new LoadingScreen("Signing in...")
            );

            try
            {
                var result = await AppState.Jellyfin.AuthenticateAsync(
                    AppState.Username,
                    password
                );

                AppState.AccessToken = result.accessToken;
                AppState.UserId = result.userId;
                AppState.Jellyfin.SetAuthToken(result.accessToken);
                AppState.Jellyfin.SetUserId(result.userId);
                AppState.SaveSession(
                    AppState.Jellyfin.ServerUrl,
                    result.accessToken,
                    result.userId,
                    AppState.Username
                );

                NavigationService.ClearStack();
                NavigationService.Navigate(
                    new HomeLoadingScreen(),
                    addToStack: false
                );
            }
            catch
            {
                // Return to password screen with clear password and show error
                _passwordInput.Text = string.Empty;
                NavigationService.NavigateBack();
                ShowErrorMessage("Invalid password. Please try again.");
            }
        }

        private void ShowErrorMessage(string message)
        {
            Console.WriteLine($"ERROR: {message}");
            _errorLabel.Text = message;
            
            // Clear error after 5 seconds
            var timer = new System.Timers.Timer(5000);
            timer.Elapsed += (sender, e) =>
            {
                _errorLabel.Text = string.Empty;
                timer.Stop();
                timer.Dispose();
            };
            timer.Start();
        }
    }
}
