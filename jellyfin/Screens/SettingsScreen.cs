using System;
using System.Collections.Generic;
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
        private View _forceFmp4Toggle;
        private TextLabel _forceFmp4Label;
        private bool _forceFmp4Enabled;
        private readonly List<View> _toggleOptions = new();
        private int _toggleIndex;

        public SettingsScreen()
        {
            _burnInEnabled = AppState.BurnInSubtitles;
            _forceFmp4Enabled = AppState.ForceFmp4Transcoding;
            Initialize();
        }

        public override void OnShow()
        {
            _toggleIndex = 0;
            FocusCurrentToggle();
            RunOnUiThread(() =>
            {
                RunOnUiThread(() =>
                {
                    _toggleIndex = 0;
                    FocusCurrentToggle();
                });
            });
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
            UiFactory.SetButtonFocusState(_burnInToggle, focused: false);

            _burnInLabel = new TextLabel(GetBurnInLabel())
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

            _forceFmp4Toggle = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 96,
                CornerRadius = 20,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BorderlineWidth = 1.5f,
                Focusable = true
            };
            UiFactory.SetButtonFocusState(_forceFmp4Toggle, focused: false);

            _forceFmp4Label = new TextLabel(GetForceFmp4Label())
            {
                TextColor = UiTheme.TextPrimary,
                PointSize = 28,
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _forceFmp4Toggle.Add(_forceFmp4Label);
            panel.Add(_forceFmp4Toggle);

            root.Add(panel);
            Add(root);

            _toggleOptions.Clear();
            _toggleOptions.Add(_burnInToggle);
            _toggleOptions.Add(_forceFmp4Toggle);
            BindToggleFocus(_burnInToggle);
            BindToggleFocus(_forceFmp4Toggle);

            _toggleIndex = 0;
            FocusCurrentToggle();
        }

        public void HandleKey(AppKey key)
        {
            switch (key)
            {
                case AppKey.Up:
                    MoveSelection(-1);
                    break;
                case AppKey.Down:
                    MoveSelection(1);
                    break;
                case AppKey.Enter:
                    ToggleCurrentOption();
                    break;
                case AppKey.Back:
                    NavigationService.NavigateBack();
                    break;
            }
        }

        private void MoveSelection(int delta)
        {
            if (_toggleOptions.Count == 0)
                return;

            _toggleIndex = Math.Clamp(_toggleIndex + delta, 0, _toggleOptions.Count - 1);
            FocusCurrentToggle();
        }

        private void FocusCurrentToggle()
        {
            if (_toggleIndex < 0 || _toggleIndex >= _toggleOptions.Count)
                return;

            FocusManager.Instance.SetCurrentFocusView(_toggleOptions[_toggleIndex]);
        }

        private void BindToggleFocus(View toggle)
        {
            toggle.FocusGained += (s, e) => UiFactory.SetButtonFocusState(toggle, focused: true);
            toggle.FocusLost += (s, e) => UiFactory.SetButtonFocusState(toggle, focused: false);
        }

        private void ToggleCurrentOption()
        {
            if (_toggleIndex == 0)
            {
                ToggleBurnIn();
                return;
            }

            if (_toggleIndex == 1)
                ToggleForceFmp4();
        }

        private void ToggleBurnIn()
        {
            _burnInEnabled = !_burnInEnabled;
            AppState.BurnInSubtitles = _burnInEnabled;
            _burnInLabel.Text = GetBurnInLabel();
        }

        private void ToggleForceFmp4()
        {
            _forceFmp4Enabled = !_forceFmp4Enabled;
            AppState.ForceFmp4Transcoding = _forceFmp4Enabled;
            _forceFmp4Label.Text = GetForceFmp4Label();
        }

        private string GetBurnInLabel()
        {
            return _burnInEnabled ? "Burn-In Subtitles: ON" : "Burn-In Subtitles: OFF";
        }

        private string GetForceFmp4Label()
        {
            return _forceFmp4Enabled ? "Force fMP4 Profile: ON" : "Force fMP4 Profile: OFF";
        }
    }
}
