using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Core;
using JellyfinTizen.UI;
using System;
using System.Net;
using System.Net.Http;

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

            var root = MonochromeAuthFactory.CreateBackground();
            var panel = MonochromeAuthFactory.CreatePanel();
            panel.Add(MonochromeAuthFactory.CreateTitle($"Sign In As {username}"));
            panel.Add(MonochromeAuthFactory.CreateSubtitle("Enter your password to continue."));

            var passwordInputShell = MonochromeAuthFactory.CreateInputFieldShell("Password", out _passwordInput);

            var hiddenInput = new PropertyMap();
            hiddenInput.Add(HiddenInputProperty.Mode, new PropertyValue((int)HiddenInputModeType.ShowLastCharacter));
            hiddenInput.Add(HiddenInputProperty.ShowLastCharacterDuration, new PropertyValue(500));
            hiddenInput.Add(HiddenInputProperty.SubstituteCharacter, new PropertyValue(0x2A));
            _passwordInput.HiddenInputSettings = hiddenInput;

            _loginButton = MonochromeAuthFactory.CreateButton("Login", out _loginText, primary: true);
            _errorLabel = MonochromeAuthFactory.CreateErrorLabel();

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
            MonochromeAuthFactory.SetButtonFocusState(_loginButton, primary: true, focused: false);
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
                MonochromeAuthFactory.SetButtonFocusState(_loginButton, primary: true, focused: true);
            }
            else
            {
                FocusManager.Instance.SetCurrentFocusView(_passwordInput);
                MonochromeAuthFactory.SetButtonFocusState(_loginButton, primary: true, focused: false);
            }
        }

        private async void Login()
        {
            var password = SanitizePassword(_passwordInput.Text);
            var username = AppState.Username?.Trim();

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
                    username,
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
            catch (HttpRequestException ex)
            {
                var errorMessage = GetFriendlyLoginError(ex);
                RunOnUiThread(() =>
                {
                    // Return to password screen with clear password and show error
                    _passwordInput.Text = string.Empty;
                    NavigationService.NavigateBack();
                    ShowErrorMessage(errorMessage);
                });
            }
            catch
            {
                RunOnUiThread(() =>
                {
                    _passwordInput.Text = string.Empty;
                    NavigationService.NavigateBack();
                    ShowErrorMessage("Sign-in failed. Please try again.");
                });
            }
        }

        private static string SanitizePassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                return string.Empty;

            // Tizen IME can occasionally leave newline characters when submitting.
            return password.Replace("\r", string.Empty).Replace("\n", string.Empty);
        }

        private static string GetFriendlyLoginError(HttpRequestException ex)
        {
            if (ex?.StatusCode == HttpStatusCode.Unauthorized ||
                ex?.StatusCode == HttpStatusCode.Forbidden)
            {
                return "Incorrect password. Check case and symbols.";
            }

            if (ex?.StatusCode == HttpStatusCode.BadRequest)
            {
                return "Sign-in request rejected. Try again.";
            }

            var raw = $"{ex?.Message} {ex?.Data?["ResponseContent"]}".ToLowerInvariant();
            if (raw.Contains("ssl") || raw.Contains("certificate"))
                return "HTTPS certificate issue on TV. Try HTTP or trusted cert.";
            if (raw.Contains("timed out") || raw.Contains("timeout"))
                return "Server timeout. Check connection and try again.";
            if (raw.Contains("name or service not known") || raw.Contains("nodename") || raw.Contains("dns"))
                return "DNS/domain issue on TV network. Check server URL.";

            return "Unable to sign in. Check server reachability.";
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
