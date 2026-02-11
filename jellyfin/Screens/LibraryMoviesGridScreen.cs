using System;
using System.Collections.Generic;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Core;
using JellyfinTizen.Models;

namespace JellyfinTizen.Screens
{
    public class LibraryMoviesGridScreen : ScreenBase, IKeyHandler
    {
        private const int TopBarHeight = 90;
        private const int ContentStartY = TopBarHeight + 20;
        private const int TopBarZ = 100;
        private const int TopBarLeftPadding = 60;
        private const int PosterWidth = 260;
        private const int PosterHeight = 390;
        private const int Spacing = 30;
        private const int SidePadding = 80;
        private const int TopBarRightPadding = 60;
        private const int RowSpacing = 50;
        private const int FocusBorder = 5;
        private const int FocusPad = 20;
        private const int TopGlowPadBoost = 8;
        private const float FocusScale = 1.14f;
        private const int CardTextHeight = 80;

        // Jellyfin Blue (#00A4DC)
        private readonly Color _focusColor = new Color(0.0f, 0.64f, 0.86f, 1.0f);
        private readonly Color _focusBorderColor = new Color(0.0f, 0.64f, 0.86f, 0.58f);

        private readonly List<JellyfinMovie> _movies;

        private readonly List<List<View>> _grid = new();
        private readonly List<View> _rowContainers = new();
        private readonly List<View> _viewports = new();

        private View _verticalContainer;

        private int _rowIndex;
        private int _colIndex;
        private int _moviesPerRow;
        private bool _focusInitialized;

        private View _settingsButton;
        private bool _settingsFocused;
        private View _settingsPanel;
        private readonly List<View> _settingsOptions = new();
        private int _settingsIndex;
        private bool _settingsVisible;

        public LibraryMoviesGridScreen(string libraryName, List<JellyfinMovie> movies)
        {
            _movies = movies;
            _moviesPerRow = CalculateColumns();

            var root = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent
            };

            var topBar = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = TopBarHeight,
                PositionZ = TopBarZ,
                BackgroundColor = Color.Black,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    CellPadding = new Size2D(16, 0)
                },
                Padding = new Extents(TopBarLeftPadding, TopBarRightPadding, 16, 0)
            };

            var sharedResPath = Tizen.Applications.Application.Current.DirectoryInfo.SharedResource;
            var leftPlaceholder = new View
            {
                WidthSpecification = 50,
                HeightSpecification = 50
            };

            var title = new TextLabel(libraryName)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                PointSize = 40,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Ellipsis = false
            };
            title.SetFontStyle(new Tizen.NUI.Text.FontStyle { Weight = FontWeightType.Bold });

            _settingsButton = new View
            {
                WidthSpecification = 50,
                HeightSpecification = 50,
                BackgroundColor = Color.Transparent,
                Focusable = true
            };

            var avatarUrl = AppState.GetUserAvatarUrl(512);
            var hasAvatar = !string.IsNullOrWhiteSpace(avatarUrl);
            var settingsIcon = new ImageView
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                ResourceUrl = hasAvatar
                    ? avatarUrl
                    : sharedResPath + "settings.png",
                FittingMode = FittingModeType.ShrinkToFit,
                SamplingMode = SamplingModeType.BoxThenLanczos,
                Padding = hasAvatar
                    ? new Extents(0, 0, 0, 0)
                    : new Extents(10, 10, 10, 10),
                AlphaMaskURL = sharedResPath + "avatar-mask.png",
                CropToMask = true,
                MaskingMode = ImageView.MaskingModeType.MaskingOnLoading
            };

            _settingsButton.Add(settingsIcon);

            topBar.Add(leftPlaceholder);
            topBar.Add(title);
            topBar.Add(_settingsButton);

            _verticalContainer = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                PositionY = ContentStartY
            };

            root.Add(_verticalContainer);
            root.Add(topBar);

            BuildGrid();

            Add(root);
            CreateSettingsPanel();
        }

        private void BuildGrid()
        {
            int index = 0;
            int y = 0;

            while (index < _movies.Count)
            {
                var cardHeight = PosterHeight + CardTextHeight;
                var viewportTopPadding = (ushort)Math.Min(FocusPad + TopGlowPadBoost, (int)ushort.MaxValue);
                var viewportBottomPadding = (ushort)Math.Max(0, FocusPad - TopGlowPadBoost);
                var viewport = new View
                {
                    WidthResizePolicy = ResizePolicyType.FillToParent,
                    HeightSpecification = cardHeight + (FocusPad * 2),
                    PositionY = y,
                    ClippingMode = ClippingModeType.ClipChildren,
                    Padding = new Extents((ushort)SidePadding, (ushort)SidePadding, viewportTopPadding, viewportBottomPadding)
                };

                var rowContainer = new View
                {
                    PositionY = 0,
                    Layout = new LinearLayout
                    {
                        LinearOrientation = LinearLayout.Orientation.Horizontal,
                        CellPadding = new Size2D(Spacing, 0)
                    }
                };

                var row = new List<View>();

                for (int i = 0; i < _moviesPerRow && index < _movies.Count; i++, index++)
                {
                    var card = CreatePosterCard(_movies[index]);
                    row.Add(card);
                    rowContainer.Add(card);
                }

                viewport.Add(rowContainer);
                _verticalContainer.Add(viewport);

                _grid.Add(row);
                _rowContainers.Add(rowContainer);
                _viewports.Add(viewport);

                y += cardHeight + (FocusPad * 2) + RowSpacing;
            }
        }

        public override void OnShow()
        {
            if (_grid.Count == 0 || _grid[0].Count == 0)
                return;

            if (!_focusInitialized)
            {
                _rowIndex = 0;
                _colIndex = 0;
                _focusInitialized = true;
            }

            _settingsFocused = false;
            _settingsVisible = false;
            _settingsPanel?.Hide();

            Highlight(true);
            FocusManager.Instance.SetCurrentFocusView(_grid[_rowIndex][_colIndex]);
            ScrollHorizontalIfNeeded();
            ScrollVerticalIfNeeded();
        }

        private int CalculateColumns()
        {
            var screenWidth = Window.Default.Size.Width;
            var usable = screenWidth - (SidePadding * 2);
            return Math.Max(1, usable / (PosterWidth + Spacing));
        }

        private View CreatePosterCard(JellyfinMovie movie)
        {
            var wrapper = new View
            {
                WidthSpecification = PosterWidth,
                HeightSpecification = PosterHeight + CardTextHeight,
                Focusable = true,
                BackgroundColor = Color.Transparent,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical
                }
            };

            var frame = new View
            {
                Name = "CardFrame",
                WidthSpecification = PosterWidth,
                HeightSpecification = PosterHeight,
                CornerRadius = 16.0f,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                ClippingMode = ClippingModeType.ClipChildren,
                BackgroundColor = Color.Transparent,
                Padding = new Extents(FocusBorder, FocusBorder, FocusBorder, FocusBorder),
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal
                }
            };

            var inner = new View
            {
                Name = "CardInner",
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                CornerRadius = 12.0f,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                ClippingMode = ClippingModeType.ClipChildren,
                BackgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f),
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal
                }
            };

            var content = new View
            {
                Name = "CardContent",
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                ClippingMode = ClippingModeType.ClipChildren
            };

            var apiKey = Uri.EscapeDataString(AppState.AccessToken);
            var serverUrl = AppState.Jellyfin.ServerUrl;
            string posterUrl = null;

            if (movie.HasPrimary)
            {
                posterUrl =
                    $"{serverUrl}/Items/{movie.Id}/Images/Primary/0" +
                    $"?maxWidth={PosterWidth}&quality=90&api_key={apiKey}";
            }
            else if (movie.HasThumb)
            {
                posterUrl =
                    $"{serverUrl}/Items/{movie.Id}/Images/Thumb/0" +
                    $"?maxWidth={PosterWidth}&quality=90&api_key={apiKey}";
            }
            else if (movie.HasBackdrop)
            {
                posterUrl =
                    $"{serverUrl}/Items/{movie.Id}/Images/Backdrop/0" +
                    $"?maxWidth={PosterWidth}&quality=85&api_key={apiKey}";
            }

            var poster = new ImageView
            {
                Name = "CardImage",
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                ResourceUrl = posterUrl,
                PreMultipliedAlpha = false   // 🔑 CRITICAL on Tizen
            };

            var titleText = new TextLabel(movie.Name)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                TextColor = Color.White,
                PointSize = 26,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word
            };

            var textContainer = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = CardTextHeight,
                BackgroundColor = Color.Transparent,
                Padding = new Extents(8, 8, 12, 0),
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical
                }
            };

            content.Add(poster);
            inner.Add(content);
            frame.Add(inner);

            textContainer.Add(titleText);
            wrapper.Add(frame);
            wrapper.Add(textContainer);

            return wrapper;
        }


        public void HandleKey(AppKey key)
        {
            if (_settingsVisible)
            {
                HandleSettingsPanelKey(key);
                return;
            }

            if (_settingsFocused)
            {
                switch (key)
                {
                    case AppKey.Down:
                        FocusSettings(false);
                        break;
                    case AppKey.Enter:
                        ShowSettingsPanel();
                        break;
                    case AppKey.Back:
                        NavigationService.NavigateBack();
                        break;
                }
                return;
            }

            switch (key)
            {
                case AppKey.Right: Move(0, 1); break;
                case AppKey.Left:  Move(0, -1); break;
                case AppKey.Down:  Move(1, 0); break;
                case AppKey.Up:
                    if (_rowIndex == 0)
                        FocusSettings(true);
                    else
                        Move(-1, 0);
                    break;
                case AppKey.Enter: OpenMovie(); break;
                case AppKey.Back:  NavigationService.NavigateBack(); break;
            }
        }

        private void Move(int rowDelta, int colDelta)
        {
            Highlight(false);

            _rowIndex = Math.Clamp(_rowIndex + rowDelta, 0, _grid.Count - 1);
            _colIndex = Math.Clamp(_colIndex + colDelta, 0, _grid[_rowIndex].Count - 1);

            Highlight(true);
            FocusManager.Instance.SetCurrentFocusView(_grid[_rowIndex][_colIndex]);

            ScrollHorizontalIfNeeded();
            ScrollVerticalIfNeeded();
        }

        private void Highlight(bool focused)
        {
            var card = _grid[_rowIndex][_colIndex];
            var frame = GetCardFrame(card);
            var content = GetCardContent(card);
            if (content != null)
                content.Scale = focused ? new Vector3(FocusScale, FocusScale, 1f) : Vector3.One;

            card.Scale = Vector3.One;
            card.PositionZ = focused ? 20 : 0;

            if (frame != null)
            {
                frame.CornerRadius = 16.0f;
                if (focused)
                {
                    frame.BackgroundColor = _focusBorderColor;
                    frame.BorderlineWidth = 2.0f;
                    frame.BorderlineColor = _focusColor;
                    frame.BoxShadow = new Shadow(12.0f, new Color(0.0f, 0.64f, 0.86f, 0.36f), new Vector2(0, 0));
                }
                else
                {
                    frame.BackgroundColor = Color.Transparent;
                    frame.BorderlineWidth = 0.0f;
                    frame.BorderlineColor = Color.Transparent;
                    frame.BoxShadow = null;
                }
            }
        }

        private View GetCardFrame(View card)
        {
            foreach (var child in card.Children)
            {
                if (child.Name == "CardFrame")
                    return child;
            }
            return null;
        }

        private View GetCardInner(View card)
        {
            foreach (var child in card.Children)
            {
                if (child.Name == "CardFrame")
                {
                    foreach (var frameChild in child.Children)
                    {
                        if (frameChild.Name == "CardInner")
                            return frameChild;
                    }
                }
            }
            return null;
        }

        private View GetCardContent(View card)
        {
            var inner = GetCardInner(card);
            if (inner == null) return null;
            foreach (var child in inner.Children)
            {
                if (child.Name == "CardContent")
                    return child;
            }
            return null;
        }


        private void FocusSettings(bool focused)
        {
            _settingsFocused = focused;

            //_settingsButton.Scale = focused ? new Vector3(1.05f, 1.05f, 1f) : Vector3.One;

            if (focused)
            {
                _settingsButton.Scale = new Vector3(1.1f, 1.1f, 1f);
                //_settingsButton.BackgroundColor = new Color(0.35f, 0.35f, 0.35f, 1f);
                Highlight(false);
                FocusManager.Instance.SetCurrentFocusView(_settingsButton);
            }
            else
            {
                _settingsButton.Scale = Vector3.One;
                //_settingsButton.BackgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
                Highlight(true);
                FocusManager.Instance.SetCurrentFocusView(_grid[_rowIndex][_colIndex]);
            }
        }

        private void CreateSettingsPanel()
        {
            _settingsPanel = new View
            {
                WidthSpecification = 420,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                BackgroundColor = new Color(0, 0, 0, 1.0f),
                PositionX = Window.Default.Size.Width - 520,
                PositionY = TopBarHeight + 16,
                PositionZ = TopBarZ + 5,
                CornerRadius = 14f,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                ClippingMode = ClippingModeType.ClipChildren,
                BorderlineWidth = 1.5f,
                BorderlineColor = new Color(0.2f, 0.2f, 0.2f, 1f),
                Padding = new Extents(16, 16, 16, 16),
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    CellPadding = new Size2D(0, 12)
                }
            };

            _settingsPanel.Hide();

            var title = new TextLabel("Settings")
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 48,
                PointSize = 26,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var list = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    CellPadding = new Size2D(0, 10)
                }
            };

            _settingsOptions.Clear();
            _settingsOptions.Add(CreateSettingsOption("Playback Settings"));
            _settingsOptions.Add(CreateSettingsOption("Logout"));
            _settingsOptions.Add(CreateSettingsOption("Switch Server"));

            foreach (var opt in _settingsOptions)
                list.Add(opt);

            _settingsPanel.Add(title);
            _settingsPanel.Add(list);

            Add(_settingsPanel);
        }

        private View CreateSettingsOption(string text)
        {
            var row = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 56,
                BackgroundColor = new Color(1, 1, 1, 0.12f),
                CornerRadius = 10f,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                ClippingMode = ClippingModeType.ClipChildren
            };

            var label = new TextLabel(text)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                PointSize = 24,
                TextColor = new Color(1, 1, 1, 0.9f),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            row.Add(label);
            return row;
        }

        private void ShowSettingsPanel()
        {
            _settingsVisible = true;
            _settingsIndex = 0;
            UpdateSettingsHighlight();
            _settingsPanel.Show();
        }

        private void HideSettingsPanel()
        {
            _settingsVisible = false;
            _settingsPanel.Hide();
        }

        private void HandleSettingsPanelKey(AppKey key)
        {
            switch (key)
            {
                case AppKey.Up:
                    _settingsIndex = Math.Clamp(_settingsIndex - 1, 0, _settingsOptions.Count - 1);
                    UpdateSettingsHighlight();
                    break;
                case AppKey.Down:
                    _settingsIndex = Math.Clamp(_settingsIndex + 1, 0, _settingsOptions.Count - 1);
                    UpdateSettingsHighlight();
                    break;
                case AppKey.Enter:
                    ActivateSettingsOption();
                    break;
                case AppKey.Back:
                    HideSettingsPanel();
                    break;
            }
        }

        private void UpdateSettingsHighlight()
        {
            for (int i = 0; i < _settingsOptions.Count; i++)
            {
                _settingsOptions[i].BackgroundColor = i == _settingsIndex
                    ? new Color(1, 1, 1, 0.22f)
                    : new Color(1, 1, 1, 0.12f);
            }
        }

        private void ActivateSettingsOption()
        {
            if (_settingsIndex == 0)
            {
                HideSettingsPanel();
                NavigationService.Navigate(new SettingsScreen());
                return;
            }

            if (_settingsIndex == 1)
            {
                _ = LogoutAsync();
                return;
            }

            if (_settingsIndex == 2)
            {
                SwitchServer();
                return;
            }

            HideSettingsPanel();
        }

        private async System.Threading.Tasks.Task LogoutAsync()
        {
            HideSettingsPanel();
            AppState.ClearSession(clearServer: false);

            NavigationService.Navigate(
                new LoadingScreen("Fetching users..."),
                addToStack: false
            );

            var users = await AppState.Jellyfin.GetPublicUsersAsync();
            NavigationService.Navigate(
                new UserSelectScreen(users),
                addToStack: false
            );
        }

        private void SwitchServer()
        {
            HideSettingsPanel();
            AppState.ClearSession(clearServer: true);
            NavigationService.Navigate(
                new ServerSetupScreen(),
                addToStack: false
            );
        }

        private void ScrollHorizontalIfNeeded()
        {
            var rowContainer = _rowContainers[_rowIndex];
            var viewport = _viewports[_rowIndex];
            var focused = _grid[_rowIndex][_colIndex];

            // FIX: Add this block to align the grid with the Title/Padding at the start
            if (_colIndex == 0)
            {
                rowContainer.PositionX = SidePadding;
                return;
            }

            var offset = -rowContainer.PositionX;
            var viewportWidth = viewport.SizeWidth;

            var left = focused.PositionX;
            var right = left + PosterWidth;

            var visibleLeft = offset;
            var visibleRight = offset + viewportWidth;

            if (right > visibleRight)
                rowContainer.PositionX -= (right - visibleRight + Spacing);
            else if (left < visibleLeft)
                rowContainer.PositionX += (visibleLeft - left + Spacing);
        }

        private void ScrollVerticalIfNeeded()
        {
            var viewportHeight = Window.Default.Size.Height;
            var rowHeight = (PosterHeight + CardTextHeight) + (FocusPad * 2) + RowSpacing;

            var currentOffset = -_verticalContainer.PositionY + ContentStartY;

            var rowTop = _rowIndex * rowHeight;
            var rowBottom = rowTop + rowHeight;

            var visibleTop = currentOffset;
            var visibleBottom = currentOffset + viewportHeight - 200;

            // Scroll down only when row hits bottom edge
            if (rowBottom > visibleBottom)
            {
                var delta = rowBottom - visibleBottom;
                _verticalContainer.PositionY -= delta;
            }
            // Scroll up only when row hits top edge
            else if (rowTop < visibleTop)
            {
                var delta = visibleTop - rowTop;
                _verticalContainer.PositionY += delta;
            }

            if (_verticalContainer.PositionY > ContentStartY)
                _verticalContainer.PositionY = ContentStartY;
        }



        private void OpenMovie()
        {
            var index = (_rowIndex * _moviesPerRow) + _colIndex;
            if (index >= _movies.Count)
                return;

            var movie = _movies[index];

            if (movie.ItemType == "Series")
            {
                NavigationService.Navigate(
                    new SeriesDetailsScreen(movie)
                );
            }
            else if (movie.ItemType == "Episode")
            {
                NavigationService.Navigate(
                    new EpisodeDetailsScreen(movie)
                );
            }
            else // Movie
            {
                NavigationService.Navigate(
                    new MovieDetailsScreen(movie)
                );
            }
        }
    }
}
