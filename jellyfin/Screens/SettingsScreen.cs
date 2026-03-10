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
        private View _forceTsToggle;
        private TextLabel _forceTsLabel;
        private bool _forceTsEnabled;
        private readonly List<View> _toggleOptions = new();
        private int _toggleIndex;

        public SettingsScreen()
        {
            _burnInEnabled = AppState.BurnInSubtitles;
            _forceTsEnabled = AppState.ForceTsTranscoding;
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
            UiFactory.SetButtonFocusState(_burnInToggle, primary: false, focused: false);

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

            _forceTsToggle = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 96,
                CornerRadius = 20,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BorderlineWidth = 1.5f,
                Focusable = true
            };
            UiFactory.SetButtonFocusState(_forceTsToggle, primary: false, focused: false);

            _forceTsLabel = new TextLabel(GetForceTsLabel())
            {
                TextColor = UiTheme.TextPrimary,
                PointSize = 28,
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _forceTsToggle.Add(_forceTsLabel);
            panel.Add(_forceTsToggle);

            root.Add(panel);
            Add(root);

            _toggleOptions.Clear();
            _toggleOptions.Add(_burnInToggle);
            _toggleOptions.Add(_forceTsToggle);
            BindToggleFocus(_burnInToggle);
            BindToggleFocus(_forceTsToggle);

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
            toggle.FocusGained += (s, e) => UiFactory.SetButtonFocusState(toggle, primary: false, focused: true);
            toggle.FocusLost += (s, e) => UiFactory.SetButtonFocusState(toggle, primary: false, focused: false);
        }

        private void ToggleCurrentOption()
        {
            if (_toggleIndex == 0)
            {
                ToggleBurnIn();
                return;
            }

            if (_toggleIndex == 1)
                ToggleForceTs();
        }

        private void ToggleBurnIn()
        {
            _burnInEnabled = !_burnInEnabled;
            AppState.BurnInSubtitles = _burnInEnabled;
            _burnInLabel.Text = GetBurnInLabel();
        }

        private void ToggleForceTs()
        {
            _forceTsEnabled = !_forceTsEnabled;
            AppState.ForceTsTranscoding = _forceTsEnabled;
            _forceTsLabel.Text = GetForceTsLabel();
        }

        private string GetBurnInLabel()
        {
            return _burnInEnabled ? "Burn-In Subtitles: ON" : "Burn-In Subtitles: OFF";
        }

        private string GetForceTsLabel()
        {
            return _forceTsEnabled ? "Force TS Profile: ON" : "Force TS Profile: OFF";
        }
    }
}
