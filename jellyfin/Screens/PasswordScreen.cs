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
        private bool _loginFocused;
        private TextLabel _errorLabel;
        private bool _loginInProgress;
        private System.Threading.Timer _errorTimer;

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
            ConfigurePasswordImeLayout();
            _passwordInput.FocusGained += (_, _) => ConfigurePasswordImeContext();

            _loginButton = MonochromeAuthFactory.CreateButton("Login", out _);
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
            ConfigurePasswordImeContext();
            _loginFocused = false;
            _loginInProgress = false;
            MonochromeAuthFactory.SetButtonFocusState(_loginButton, focused: false);
        }

        public override void OnHide()
        {
            _loginInProgress = false;
            DisposeTimer(ref _errorTimer);
        }

        private void ConfigurePasswordImeLayout()
        {
            try
            {
                var inputMethod = new InputMethod
                {
                    PanelLayout = InputMethod.PanelLayoutType.Password,
                    PasswordVariation = InputMethod.PasswordLayoutType.WithNumberOnly,
                    ActionButton = InputMethod.ActionButtonTitleType.Login,
                    AutoCapital = InputMethod.AutoCapitalType.None
                };
                _passwordInput.InputMethodSettings = inputMethod.OutputMap;
            }
            catch
            {
            }
        }

        private void ConfigurePasswordImeContext()
        {
            try
            {
                var ime = _passwordInput.GetInputMethodContext();
                if (ime == null)
                    return;

                ime.SetInputPanelLanguage(InputMethodContext.InputPanelLanguage.Automatic);
                ime.TextPrediction = false;
                ime.SetReturnKeyState(true);
            }
            catch
            {
            }
        }

        public void HandleKey(AppKey key)
        {
            if (_loginInProgress)
                return;

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
                        FireAndForget(LoginAsync());
                    break;
            }
        }

        private void FocusLogin(bool focused)
        {
            _loginFocused = focused;

            if (focused)
            {
                FocusManager.Instance.SetCurrentFocusView(_loginButton);
                MonochromeAuthFactory.SetButtonFocusState(_loginButton, focused: true);
            }
            else
            {
                FocusManager.Instance.SetCurrentFocusView(_passwordInput);
                MonochromeAuthFactory.SetButtonFocusState(_loginButton, focused: false);
            }
        }

        private async System.Threading.Tasks.Task LoginAsync()
        {
            if (_loginInProgress)
                return;

            var password = SanitizePassword(_passwordInput.Text);
            var username = AppState.Username?.Trim();

            if (string.IsNullOrEmpty(password))
            {
                ShowErrorMessage("Password required.");
                return;
            }

            _loginInProgress = true;
            DisposeTimer(ref _errorTimer);
            _errorLabel.Text = string.Empty;

            try
            {
                RunOnUiThread(() =>
                {
                    NavigationService.Navigate(
                        new LoadingScreen("Signing in...")
                    );
                });

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
            finally
            {
                _loginInProgress = false;
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
            var responseContent = ex?.Data?["ResponseContent"]?.ToString() ?? string.Empty;
            var requestPath = ex?.Data?["RequestPath"]?.ToString() ?? string.Empty;
            var raw = $"{ex?.Message} {responseContent}".ToLowerInvariant();
            string snippet = responseContent;
            if (!string.IsNullOrWhiteSpace(snippet) && snippet.Length > 140)
                snippet = snippet.Substring(0, 140) + "...";

            if (ex?.StatusCode == HttpStatusCode.Unauthorized ||
                ex?.StatusCode == HttpStatusCode.Forbidden)
            {
                if (raw.Contains("invalid token") || raw.Contains("customauthentication"))
                    return "Server auth plugin rejected this sign-in request (not a password typo).";

                if (!string.IsNullOrWhiteSpace(snippet))
                    return $"Sign-in unauthorized: {snippet}";

                if (!string.IsNullOrWhiteSpace(requestPath))
                    return $"Sign-in unauthorized on {requestPath} (401/403).";

                return "Sign-in unauthorized by server (401/403). Check credentials and auth plugin settings.";
            }

            if (ex?.StatusCode == HttpStatusCode.BadRequest)
            {
                return "Sign-in request rejected. Try again.";
            }

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
            ShowTransientMessage(_errorLabel, message, ref _errorTimer);
        }
    }
}
