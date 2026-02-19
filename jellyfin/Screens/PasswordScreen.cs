using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Core;
using JellyfinTizen.UI;
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

            var root = UiFactory.CreateAtmosphericBackground();
            var panel = UiFactory.CreateCenteredPanel();
            panel.Add(UiFactory.CreateDisplayTitle($"Sign In As {username}"));
            panel.Add(UiFactory.CreateSubtitle("Enter your password to continue."));

            var passwordInputShell = UiFactory.CreateInputFieldShell("Password", out _passwordInput);

            var hiddenInput = new PropertyMap();
            hiddenInput.Add(HiddenInputProperty.Mode, new PropertyValue((int)HiddenInputModeType.ShowLastCharacter));
            hiddenInput.Add(HiddenInputProperty.ShowLastCharacterDuration, new PropertyValue(500));
            hiddenInput.Add(HiddenInputProperty.SubstituteCharacter, new PropertyValue(0x2A));
            _passwordInput.HiddenInputSettings = hiddenInput;

            _loginButton = UiFactory.CreateButton("Login", out _loginText, primary: true);
            _errorLabel = UiFactory.CreateErrorLabel();

            panel.Add(passwordInputShell);
            panel.Add(_loginButton);
            panel.Add(_errorLabel);
            root.Add(panel);
            Add(root);
        }

        public override void OnShow()
        {
            _passwordInput.Text = string.Empty;
            FocusManager.Instance.SetCurrentFocusView(_passwordInput);
            _loginFocused = false;
            UiFactory.SetButtonFocusState(_loginButton, primary: true, focused: false);
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
                UiFactory.SetButtonFocusState(_loginButton, primary: true, focused: true);
            }
            else
            {
                FocusManager.Instance.SetCurrentFocusView(_passwordInput);
                UiFactory.SetButtonFocusState(_loginButton, primary: true, focused: false);
            }
        }

        private async void Login()
        {
            var password = _passwordInput.Text;

            if (string.IsNullOrEmpty(password))
                return;

            RunOnUiThread(() =>
            {
                NavigationService.Navigate(
                    new LoadingScreen("Signing in...")
                );
            });

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

                RunOnUiThread(() =>
                {
                    NavigationService.ClearStack();
                    NavigationService.Navigate(
                        new HomeLoadingScreen(),
                        addToStack: false
                    );
                });
            }
            catch
            {
                RunOnUiThread(() =>
                {
                    // Return to password screen with clear password and show error
                    _passwordInput.Text = string.Empty;
                    NavigationService.NavigateBack();
                    ShowErrorMessage("Invalid password. Please try again.");
                });
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
