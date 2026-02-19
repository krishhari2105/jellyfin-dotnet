using System;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using Tizen.NUI.Components;
using JellyfinTizen.Core;
using JellyfinTizen.UI;

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
            var root = UiFactory.CreateAtmosphericBackground();
            var panel = UiFactory.CreateCenteredPanel(width: 960, top: 140);
            panel.Add(UiFactory.CreateDisplayTitle("Settings"));
            panel.Add(UiFactory.CreateSubtitle("Playback preferences"));

            _burnInToggle = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 96,
                CornerRadius = 20,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BorderlineWidth = 1.5f,
                Focusable = true
            };
            UiFactory.SetButtonFocusState(_burnInToggle, primary: false, focused: false);

            _burnInLabel = new TextLabel(_burnInEnabled ? "Burn-In Subtitles: ON" : "Burn-In Subtitles: OFF")
            {
                TextColor = UiTheme.TextPrimary,
                PointSize = 28,
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _burnInToggle.Add(_burnInLabel);
            panel.Add(_burnInToggle);
            root.Add(panel);
            Add(root);
            
            _burnInToggle.FocusGained += (s, e) =>
            {
                UiFactory.SetButtonFocusState(_burnInToggle, primary: false, focused: true);
            };

            _burnInToggle.FocusLost += (s, e) =>
            {
                UiFactory.SetButtonFocusState(_burnInToggle, primary: false, focused: false);
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
            _burnInToggle.BackgroundColor = _burnInEnabled
                ? new Color(0.00f, 164f / 255f, 220f / 255f, 0.30f)
                : UiTheme.SurfaceMuted;
        }
    }
}
