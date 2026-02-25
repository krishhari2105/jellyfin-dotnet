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
        private const int AvatarSize = 126;
        private const int AvatarFetchSize = AvatarSize * 2;
        private const int AvatarImageInset = 0;
        private const float AvatarFocusRingWidth = 4.0f;
        private const float AvatarImageOverscan = 1.10f;
        private const int TileWidth = 196;
        private const int TileHeight = 222;
        private const int TileSpacing = 30;
        private const int NameTopGap = 18;
        private const int NameHeight = 78;
        private const int ViewportTopInset = 14;
        private const int ViewportBottomInset = 8;
        private const int ViewportHeight = TileHeight + ViewportTopInset + ViewportBottomInset;
        private const int ScrollEdgePadding = 52;
        private const int FallbackViewportWidth = 640;

        private sealed class ProfileTile
        {
            public JellyfinUser User;
            public View Root;
            public View Avatar;
            public View AvatarFocusRing;
            public View AvatarImageHost;
            public ImageView AvatarImage;
            public bool HasAvatarImage;
            public TextLabel Initials;
            public TextLabel Name;
        }

        private readonly List<ProfileTile> _profiles = new List<ProfileTile>();
        private View _profilesViewport;
        private View _profilesRow;
        private int _profilesContentWidth;
        private int _focusedIndex = 0;

        public UserSelectScreen(List<JellyfinUser> users)
        {
            var root = MonochromeAuthFactory.CreateBackground();
            var panel = MonochromeAuthFactory.CreatePanel();
            panel.Add(MonochromeAuthFactory.CreateTitle("Who's Watching?"));
            panel.Add(MonochromeAuthFactory.CreateSubtitle("Select a profile."));

            _profilesViewport = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = ViewportHeight,
                ClippingMode = ClippingModeType.ClipChildren
            };

            _profilesRow = new View
            {
                PositionY = ViewportTopInset,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    CellPadding = new Size2D(TileSpacing, 0)
                }
            };

            foreach (var user in users ?? new List<JellyfinUser>())
            {
                var tile = CreateUserTile(user);
                _profiles.Add(tile);
                _profilesRow.Add(tile.Root);
            }

            if (_profiles.Count == 0)
            {
                _profilesViewport.Add(new TextLabel("No profiles found")
                {
                    WidthResizePolicy = ResizePolicyType.FillToParent,
                    HeightResizePolicy = ResizePolicyType.FillToParent,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    PointSize = 24.0f,
                    TextColor = new Color(1f, 1f, 1f, 0.72f)
                });
            }
            else
            {
                _profilesContentWidth = (_profiles.Count * TileWidth) + ((_profiles.Count - 1) * TileSpacing);
                _profilesRow.WidthSpecification = _profilesContentWidth;
                _profilesViewport.Add(_profilesRow);
            }

            panel.Add(_profilesViewport);
            root.Add(panel);
            Add(root);
        }

        public override void OnShow()
        {
            if (_profiles.Count > 0)
            {
                _focusedIndex = Math.Clamp(_focusedIndex, 0, _profiles.Count - 1);
                for (int i = 0; i < _profiles.Count; i++)
                    ApplyFocusState(_profiles[i], focused: false);

                ApplyFocusState(_profiles[_focusedIndex], focused: true);
                FocusManager.Instance.SetCurrentFocusView(_profiles[_focusedIndex].Root);
                EnsureFocusedVisible(centerWhenNoOverflow: true);
            }
        }

        private ProfileTile CreateUserTile(JellyfinUser user)
        {
            string userName = string.IsNullOrWhiteSpace(user?.Name) ? "User" : user.Name;
            string initials = GetUserInitials(userName);
            string avatarUrl = BuildUserAvatarUrl(user, AvatarFetchSize);

            var tileRoot = new View
            {
                WidthSpecification = TileWidth,
                HeightSpecification = TileHeight,
                Focusable = true,
                PositionZ = 0
            };

            var avatar = new View
            {
                WidthSpecification = AvatarSize,
                HeightSpecification = AvatarSize,
                PositionX = (TileWidth - AvatarSize) / 2,
                PositionY = 0,
                CornerRadius = AvatarSize / 2.0f,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BackgroundColor = Color.Transparent,
                ClippingMode = ClippingModeType.ClipChildren
            };

            int avatarImageSize = AvatarSize - (AvatarImageInset * 2);
            var avatarImageHost = new View
            {
                WidthSpecification = avatarImageSize,
                HeightSpecification = avatarImageSize,
                PositionX = AvatarImageInset,
                PositionY = AvatarImageInset,
                CornerRadius = avatarImageSize / 2.0f,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BackgroundColor = new Color(1f, 1f, 1f, 0.10f),
                ClippingMode = ClippingModeType.ClipChildren
            };
            avatar.Add(avatarImageHost);

            var avatarFocusRing = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                CornerRadius = AvatarSize / 2.0f,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BackgroundColor = Color.Transparent,
                BorderlineColor = new Color(1f, 1f, 1f, 0.96f),
                BorderlineWidth = 0.0f
            };
            avatar.Add(avatarFocusRing);

            var initialsLabel = new TextLabel(initials)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                PointSize = 30.0f,
                TextColor = Color.White,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            avatarImageHost.Add(initialsLabel);
            
            ImageView avatarImage = null;
            bool hasAvatarImage = !string.IsNullOrWhiteSpace(avatarUrl);
            if (hasAvatarImage)
            {
                string sharedResPath = Tizen.Applications.Application.Current.DirectoryInfo.SharedResource;
                avatarImage = new ImageView
                {
                    WidthResizePolicy = ResizePolicyType.FillToParent,
                    HeightResizePolicy = ResizePolicyType.FillToParent,
                    PositionUsesPivotPoint = true,
                    ParentOrigin = Tizen.NUI.ParentOrigin.Center,
                    PivotPoint = Tizen.NUI.PivotPoint.Center,
                    Scale = new Vector3(AvatarImageOverscan, AvatarImageOverscan, 1f),
                    ResourceUrl = avatarUrl,
                    BackgroundColor = Color.Transparent,
                    PreMultipliedAlpha = false,
                    FittingMode = FittingModeType.ScaleToFill,
                    SamplingMode = SamplingModeType.BoxThenLanczos,
                    AlphaMaskURL = sharedResPath + "avatar-mask.png",
                    CropToMask = true,
                    MaskingMode = ImageView.MaskingModeType.MaskingOnLoading
                };
                avatarImageHost.Add(avatarImage);
                initialsLabel.Opacity = 0.0f;
            }

            var nameLabel = new TextLabel(userName)
            {
                WidthSpecification = TileWidth,
                HeightSpecification = NameHeight,
                PositionY = AvatarSize + NameTopGap,
                PointSize = 22.0f,
                TextColor = new Color(1f, 1f, 1f, 0.78f),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Center,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word
            };

            tileRoot.Add(avatar);
            tileRoot.Add(nameLabel);

            var tile = new ProfileTile
            {
                User = user,
                Root = tileRoot,
                Avatar = avatar,
                AvatarFocusRing = avatarFocusRing,
                AvatarImageHost = avatarImageHost,
                AvatarImage = avatarImage,
                HasAvatarImage = hasAvatarImage,
                Initials = initialsLabel,
                Name = nameLabel
            };
            ApplyFocusState(tile, focused: false);
            return tile;
        }

        private static string BuildUserAvatarUrl(JellyfinUser user, int size)
        {
            if (user == null || string.IsNullOrWhiteSpace(user.Id))
                return null;

            string serverUrl = AppState.Jellyfin?.ServerUrl;
            if (string.IsNullOrWhiteSpace(serverUrl))
                serverUrl = AppState.ServerUrl;

            if (string.IsNullOrWhiteSpace(serverUrl))
                return null;

            if (size <= 0)
                size = 96;

            string url =
                $"{serverUrl.TrimEnd('/')}/Users/{Uri.EscapeDataString(user.Id)}/Images/Primary" +
                $"?width={size}&height={size}&fillWidth={size}&fillHeight={size}&quality=95";

            if (!string.IsNullOrWhiteSpace(user.PrimaryImageTag))
                url += $"&tag={Uri.EscapeDataString(user.PrimaryImageTag)}";

            string token = AppState.AccessToken;
            if (string.IsNullOrWhiteSpace(token))
                token = AppState.Jellyfin?.AccessToken;

            if (!string.IsNullOrWhiteSpace(token))
                url += $"&api_key={Uri.EscapeDataString(token)}";

            return url;
        }

        private static string GetUserInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "U";

            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return "U";
            if (parts.Length == 1)
                return parts[0].Substring(0, 1).ToUpperInvariant();

            return (parts[0].Substring(0, 1) + parts[1].Substring(0, 1)).ToUpperInvariant();
        }

        private void ApplyFocusState(ProfileTile tile, bool focused)
        {
            if (tile == null)
                return;

            tile.Root.PositionZ = focused ? 20 : 0;
            tile.Avatar.Scale = focused ? new Vector3(1.08f, 1.08f, 1f) : Vector3.One;
            tile.Avatar.BackgroundColor = Color.Transparent;
            tile.Avatar.BorderlineWidth = 0.0f;
            tile.AvatarFocusRing.BorderlineWidth = focused ? AvatarFocusRingWidth : 0.0f;
            tile.AvatarImageHost.BackgroundColor = tile.HasAvatarImage
                ? Color.Transparent
                : (focused ? UiTheme.MediaCardFocusFill : new Color(1f, 1f, 1f, 0.10f));
            tile.Initials.TextColor = focused && !tile.HasAvatarImage ? Color.Black : Color.White;
            tile.Name.TextColor = focused
                ? new Color(1f, 1f, 1f, 0.98f)
                : new Color(1f, 1f, 1f, 0.78f);
        }

        private void MoveFocus(int delta)
        {
            if (_profiles.Count == 0)
                return;

            int next = Math.Clamp(_focusedIndex + delta, 0, _profiles.Count - 1);
            if (next == _focusedIndex)
                return;

            ApplyFocusState(_profiles[_focusedIndex], focused: false);
            _focusedIndex = next;
            ApplyFocusState(_profiles[_focusedIndex], focused: true);
            FocusManager.Instance.SetCurrentFocusView(_profiles[_focusedIndex].Root);
            EnsureFocusedVisible();
        }

        private void EnsureFocusedVisible(bool centerWhenNoOverflow = false)
        {
            if (_profiles.Count == 0 || _profilesRow == null || _profilesViewport == null)
                return;

            float viewportWidth = _profilesViewport.SizeWidth > 0
                ? _profilesViewport.SizeWidth
                : FallbackViewportWidth;
            if (viewportWidth <= 0)
                return;

            if (_profilesContentWidth <= viewportWidth)
            {
                if (centerWhenNoOverflow)
                    _profilesRow.PositionX = (viewportWidth - _profilesContentWidth) / 2;
                return;
            }

            float targetX = _profilesRow.PositionX;
            float itemLeft = _focusedIndex * (TileWidth + TileSpacing);
            float itemRight = itemLeft + TileWidth;
            float visibleLeft = -_profilesRow.PositionX;
            float visibleRight = visibleLeft + viewportWidth;

            if (itemRight > visibleRight - ScrollEdgePadding)
                targetX -= itemRight - (visibleRight - ScrollEdgePadding);
            else if (itemLeft < visibleLeft + ScrollEdgePadding)
                targetX += (visibleLeft + ScrollEdgePadding) - itemLeft;

            float minX = viewportWidth - _profilesContentWidth;
            if (targetX < minX)
                targetX = minX;
            if (targetX > 0)
                targetX = 0;

            _profilesRow.PositionX = targetX;
        }

        private void OnUserSelected(int index)
        {
            if (index < 0 || index >= _profiles.Count)
                return;

            var userName = _profiles[index].User?.Name ?? "User";
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
                case AppKey.Left:
                case AppKey.Up:
                    MoveFocus(-1);
                    break;
                case AppKey.Right:
                case AppKey.Down:
                    MoveFocus(1);
                    break;
                case AppKey.Enter:
                    OnUserSelected(_focusedIndex);
                    break;
            }
        }

    }
}
