using System;
using System.Collections.Generic;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Core;
using JellyfinTizen.Models;
using JellyfinTizen.UI;

namespace JellyfinTizen.Screens
{
    public class UserSelectScreen : ScreenBase, IKeyHandler
    {
        private readonly List<View> _userViews = new List<View>();
        private int _focusedIndex = 0;

        public UserSelectScreen(List<JellyfinUser> users)
        {
            var root = UiFactory.CreateAtmosphericBackground();
            var panel = UiFactory.CreateCenteredPanel();
            panel.Add(UiFactory.CreateDisplayTitle("Who's Watching?"));
            panel.Add(UiFactory.CreateSubtitle("Select a profile."));

            var list = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    CellPadding = new Size2D(0, 16)
                }
            };

            foreach (var user in users)
            {
                var item = CreateUserItem(user);
                _userViews.Add(item);
                list.Add(item);
            }

            panel.Add(list);
            root.Add(panel);
            Add(root);
        }

        public override void OnShow()
        {
            if (_userViews.Count > 0)
            {
                Highlight(0);
            }
        }

        private View CreateUserItem(JellyfinUser user)
        {
            var view = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = UiTheme.ListItemHeight,
                Focusable = true,
                CornerRadius = 16.0f,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BorderlineWidth = 1.5f
            };
            UiFactory.SetButtonFocusState(view, primary: false, focused: false);

            var label = new TextLabel(user.Name)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                PointSize = 30,
                TextColor = UiTheme.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            view.Add(label);
            return view;
        }

        private void MoveFocus(int delta)
        {
            Highlight(_focusedIndex, false);
            _focusedIndex = Math.Clamp(_focusedIndex + delta, 0, _userViews.Count - 1);
            Highlight(_focusedIndex, true);
        }

        private void Highlight(int index, bool focused = true)
        {
            var view = _userViews[index];
            UiFactory.SetButtonFocusState(view, primary: false, focused: focused);
        }

        private void OnUserSelected(int index)
        {
        var userName =
        ((TextLabel)_userViews[index].Children[0]).Text;

        NavigationService.NavigateWithLoading(
        () => new PasswordScreen(userName),
        "Loading sign in..."
        );
        }


        public void HandleKey(AppKey key)
        {
            switch (key)
            {
                case AppKey.Back:
                    NavigationService.NavigateBack();
                    return;
                case AppKey.Down:
                    MoveFocus(1);
                    break;
                case AppKey.Up:
                    MoveFocus(-1);
                    break;
                case AppKey.Enter:
                    OnUserSelected(_focusedIndex);
                    break;
            }
        }

    }
}
