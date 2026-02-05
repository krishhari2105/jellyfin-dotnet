using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Core;

namespace JellyfinTizen.Screens
{
    public class ServerSetupScreen : ScreenBase, IKeyHandler
    {
        private TextField _serverInput;
        private View _continueButton;
        private TextLabel _continueText;

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

            container.Add(title);
            container.Add(_serverInput);
            container.Add(_continueButton);
            Add(container);
        }

        public override void OnShow()
        {
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

            AppState.SaveServer(url);
            AppState.Jellyfin.Connect(url);
            NavigationService.Navigate(new LoadingScreen("Fetching users..."));

            var users = await AppState.Jellyfin.GetPublicUsersAsync();
            NavigationService.Navigate(
                new UserSelectScreen(users),
                addToStack: false
            );
        }
    }
}
