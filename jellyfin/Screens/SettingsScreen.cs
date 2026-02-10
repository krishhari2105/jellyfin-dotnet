using System;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using Tizen.NUI.Components;
using JellyfinTizen.Core;

namespace JellyfinTizen.Screens
{
    public class SettingsScreen : ScreenBase, IKeyHandler
    {
        private View _burnInToggle;
        private TextLabel _burnInLabel;
        private bool _burnInEnabled;

        public SettingsScreen()
        {
            _burnInEnabled = AppState.BurnInSubtitles;
            Initialize();
        }

        private void Initialize()
        {
            var root = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                BackgroundColor = Color.Black
            };
            Add(root);

            var title = new TextLabel("Settings")
            {
                TextColor = Color.White,
                PointSize = 64,
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HorizontalAlignment = HorizontalAlignment.Center,
                PositionY = 80
            };
            root.Add(title);

            _burnInToggle = new View
            {
                WidthSpecification = 500,
                HeightSpecification = 100,
                BackgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f),
                CornerRadius = 50,
                PositionX = (Window.Default.Size.Width - 500) / 2,
                PositionY = 240,
                Focusable = true
            };
            _burnInLabel = new TextLabel(_burnInEnabled ? "Burn-In Subtitles: ON" : "Burn-In Subtitles: OFF")
            {
                TextColor = Color.White,
                PointSize = 34,
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _burnInToggle.Add(_burnInLabel);
            root.Add(_burnInToggle);
            
            _burnInToggle.FocusGained += (s, e) =>
            {
                _burnInToggle.Scale = new Vector3(1.05f, 1.05f, 1.0f);
            };

            _burnInToggle.FocusLost += (s, e) =>
            {
                _burnInToggle.Scale = Vector3.One;
            };

            FocusManager.Instance.SetCurrentFocusView(_burnInToggle);
        }

        public void HandleKey(AppKey key)
        {
            switch (key)
            {
                case AppKey.Enter:
                    ToggleBurnIn();
                    break;
                case AppKey.Back:
                    NavigationService.NavigateBack();
                    break;
            }
        }

        private void ToggleBurnIn()
        {
            _burnInEnabled = !_burnInEnabled;
            AppState.BurnInSubtitles = _burnInEnabled;
            _burnInLabel.Text = _burnInEnabled ? "Burn-In Subtitles: ON" : "Burn-In Subtitles: OFF";
            _burnInToggle.BackgroundColor = _burnInEnabled ? new Color(0.85f, 0.11f, 0.11f, 1f) : new Color(0.2f, 0.2f, 0.2f, 1f);
        }
    }
}
