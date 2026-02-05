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
        private const int PosterWidth = 260;
        private const int PosterHeight = 390;
        private const int Spacing = 30;
        private const int SidePadding = 80;
        private const int RowSpacing = 50;

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
                HeightSpecification = 80,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    CellPadding = new Size2D(20, 0)
                },
                Padding = new Extents(SidePadding, SidePadding, 20, 0)
            };

            var title = new TextLabel(libraryName)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                PointSize = 52,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Begin
            };

            _settingsButton = new View
            {
                WidthSpecification = 200,
                HeightSpecification = 70,
                BackgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f),
                Focusable = true
            };

            var settingsLabel = new TextLabel("Settings")
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                PointSize = 28,
                TextColor = Color.White
            };

            _settingsButton.Add(settingsLabel);

            topBar.Add(title);
            topBar.Add(_settingsButton);

            _verticalContainer = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                PositionY = 120
            };

            root.Add(topBar);
            root.Add(_verticalContainer);

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
                var viewport = new View
                {
                    WidthResizePolicy = ResizePolicyType.FillToParent,
                    HeightSpecification = PosterHeight,
                    PositionY = y,
                    ClippingMode = ClippingModeType.ClipChildren,
                    Padding = new Extents(SidePadding, SidePadding, 0, 0)
                };

                var rowContainer = new View
                {
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

                y += PosterHeight + RowSpacing;
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
            var card = new View
            {
                WidthSpecification = PosterWidth,
                HeightSpecification = PosterHeight,
                Focusable = true
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
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                ResourceUrl = posterUrl,
                PreMultipliedAlpha = false   // 🔑 CRITICAL on Tizen
            };

            var titleOverlay = new TextLabel(movie.Name)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 80,
                PositionY = PosterHeight - 80,
                BackgroundColor = new Color(0, 0, 0, 0.65f),
                TextColor = Color.White,
                PointSize = 22,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word
            };

            card.Add(poster);
            card.Add(titleOverlay);

            return card;
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
            _grid[_rowIndex][_colIndex].Scale =
                focused ? new Vector3(1.08f, 1.08f, 1f) : Vector3.One;
        }

        private void FocusSettings(bool focused)
        {
            _settingsFocused = focused;

            _settingsButton.BackgroundColor = focused
                ? new Color(0.35f, 0.35f, 0.35f, 1f)
                : new Color(0.2f, 0.2f, 0.2f, 1f);

            if (focused)
            {
                Highlight(false);
                FocusManager.Instance.SetCurrentFocusView(_settingsButton);
            }
            else
            {
                Highlight(true);
                FocusManager.Instance.SetCurrentFocusView(_grid[_rowIndex][_colIndex]);
            }
        }

        private void CreateSettingsPanel()
        {
            _settingsPanel = new View
            {
                WidthSpecification = 420,
                HeightSpecification = 260,
                BackgroundColor = new Color(0, 0, 0, 0.9f),
                PositionX = Window.Default.Size.Width - 520,
                PositionY = 120
            };

            _settingsPanel.Hide();

            var title = new TextLabel("Settings")
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 60,
                PointSize = 28,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var list = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                PositionY = 70,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    CellPadding = new Size2D(0, 10)
                }
            };

            _settingsOptions.Clear();
            _settingsOptions.Add(CreateSettingsOption("Logout"));
            _settingsOptions.Add(CreateSettingsOption("Switch Server"));
            _settingsOptions.Add(CreateSettingsOption("Close"));

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
                HeightSpecification = 60,
                BackgroundColor = new Color(1, 1, 1, 0.1f)
            };

            var label = new TextLabel(text)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                PointSize = 26,
                TextColor = new Color(1, 1, 1, 0.85f),
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
                    ? new Color(1, 1, 1, 0.2f)
                    : new Color(1, 1, 1, 0.1f);
            }
        }

        private void ActivateSettingsOption()
        {
            if (_settingsIndex == 0)
            {
                _ = LogoutAsync();
                return;
            }

            if (_settingsIndex == 1)
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
            var rowHeight = PosterHeight + RowSpacing;

            var currentOffset = -_verticalContainer.PositionY + 120;

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
