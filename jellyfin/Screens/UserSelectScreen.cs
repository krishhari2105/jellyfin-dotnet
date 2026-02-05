using System;
using System.Collections.Generic;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Core;
using JellyfinTizen.Models;

namespace JellyfinTizen.Screens
{
    public class UserSelectScreen : ScreenBase, IKeyHandler
    {
        private readonly List<View> _userViews = new List<View>();
        private int _focusedIndex = 0;

        public UserSelectScreen(List<JellyfinUser> users)
        {
            var container = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    CellPadding = new Size2D(0, 30)
                }
            };

            var title = new TextLabel("Who's watching?")
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                PointSize = 56,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            container.Add(title);

            foreach (var user in users)
            {
                var item = CreateUserItem(user);
                _userViews.Add(item);
                container.Add(item);
            }

            Add(container);
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
                HeightSpecification = 90,
                BackgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f),
                Focusable = true
            };

            var label = new TextLabel(user.Name)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                PointSize = 40,
                TextColor = Color.White,
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
            _userViews[index].BackgroundColor =
                focused ? new Color(0.35f, 0.35f, 0.35f, 1f)
                        : new Color(0.2f, 0.2f, 0.2f, 1f);
        }

        private void OnUserSelected(int index)
        {
        var userName =
        ((TextLabel)_userViews[index].Children[0]).Text;

        NavigationService.Navigate(
        new PasswordScreen(userName)
        );
        }


        public void HandleKey(AppKey key)
        {
            switch (key)
            {
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
