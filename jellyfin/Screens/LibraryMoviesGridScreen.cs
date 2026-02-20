using System;
using System.Collections.Generic;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Core;
using JellyfinTizen.Models;
using JellyfinTizen.UI;
using JellyfinTizen.Utils;

namespace JellyfinTizen.Screens
{
    public class LibraryMoviesGridScreen : ScreenBase, IKeyHandler
    {
        private const int TopBarHeight = UiTheme.HomeTopBarHeight;
        private const int ContentStartY = TopBarHeight + UiTheme.HomeRowsTopGap;
        private const int TopBarZ = 10;
        private const int TopBarLeftPadding = UiTheme.HomeSidePadding;
        private const int PosterWidth = 260;
        private const int PosterHeight = 390;
        private const int Spacing = UiTheme.LibraryCardSpacing;
        private const int SidePadding = UiTheme.LibrarySidePadding;
        private const int TopBarRightPadding = UiTheme.HomeSidePadding;
        private const int RowSpacing = UiTheme.LibraryRowSpacing;
        private const int FocusBorder = UiTheme.LibraryFocusBorder;
        private const int FocusPad = UiTheme.LibraryFocusPad;
        private const int ContentViewportTopInset = 4;
        private const int ContentViewportStartY = TopBarHeight + ContentViewportTopInset;
        private const int TopGlowPadBoost = UiTheme.LibraryTopGlowPadBoost;
        private const float FocusScale = 1.08f;
        private static readonly bool UseLightweightFocusMode = true;
        private const int CardTextHeight = 80;
        private const int RowBuildBatchSize = 2;
        private const int PosterVisibleRowBuffer = 1;
        private const int PosterKeepLowRowBuffer = 3;
        private const int PosterRefreshIntervalMs = 260;
        private const int BuildTickMs = 20;
        private const int HighQualityDelayMs = 320;

        private readonly Color _focusColor = UiTheme.MediaCardFocusBorder;
        private readonly Color _focusBorderColor = UiTheme.MediaCardFocusFill;

        private readonly List<JellyfinMovie> _movies;

        private readonly List<List<View>> _grid = new();
        private readonly List<View> _rowContainers = new();
        private readonly List<View> _viewports = new();
        private readonly Dictionary<View, Animation> _focusAnimations = new();
        private readonly Dictionary<ImageView, PosterLoadState> _posterStates = new();

        private View _contentViewport;
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
        private int _settingsPanelBaseX;

        private Animation _horizontalScrollAnimation;
        private Animation _verticalScrollAnimation;
        private Timer _buildTimer;
        private Timer _posterRefreshTimer;
        private Timer _highQualityDelayTimer;
        private int _nextMovieIndexToBuild;
        private int _nextRowY;
        private bool _isGridBuildCompleted;
        private bool _allowHighQualityUpgrade;

        private sealed class PosterLoadState
        {
            public string LowUrl;
            public string HighUrl;
            public int Row;
            public PosterQuality Quality;
        }

        private enum PosterQuality
        {
            Unloaded = 0,
            Low = 1,
            High = 2
        }

        public LibraryMoviesGridScreen(string libraryName, List<JellyfinMovie> movies)
        {
            _movies = movies;
            _moviesPerRow = CalculateColumns();

            var root = UiFactory.CreateAtmosphericBackground();

            var topBar = MediaBrowserChrome.CreateTopBar(
                libraryName,
                TopBarHeight,
                TopBarLeftPadding,
                TopBarRightPadding,
                positionZ: TopBarZ,
                centerTitle: false,
                includeLeftSpacer: false,
                leftBlendOffsetX: -190,
                leftBlendOffsetY: -140,
                out _settingsButton
            );

            _contentViewport = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = Math.Max(0, Window.Default.Size.Height - ContentViewportStartY),
                PositionY = ContentViewportStartY,
                ClippingMode = ClippingModeType.ClipChildren
            };

            _verticalContainer = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                PositionY = 0
            };

            _contentViewport.Add(_verticalContainer);
            root.Add(_contentViewport);
            root.Add(topBar);

            Add(root);
            CreateSettingsPanel();
        }

        private void BuildGridBatch(int rowsToBuild)
        {
            if (_isGridBuildCompleted || rowsToBuild <= 0)
            {
                return;
            }

            var cardHeight = PosterHeight + CardTextHeight;
            var rowHeight = cardHeight + (FocusPad * 2) + RowSpacing;
            int builtRows = 0;

            while (_nextMovieIndexToBuild < _movies.Count && builtRows < rowsToBuild)
            {
                var viewportTopPadding = (ushort)Math.Min(FocusPad + TopGlowPadBoost, (int)ushort.MaxValue);
                var viewportBottomPadding = (ushort)Math.Max(0, FocusPad - TopGlowPadBoost);
                var viewport = new View
                {
                    WidthResizePolicy = ResizePolicyType.FillToParent,
                    HeightSpecification = cardHeight + (FocusPad * 2),
                    PositionY = _nextRowY,
                    ClippingMode = ClippingModeType.ClipChildren,
                    Padding = new Extents((ushort)SidePadding, (ushort)SidePadding, viewportTopPadding, viewportBottomPadding)
                };

                var rowContainer = new View
                {
                    PositionX = SidePadding,
                    PositionY = 0,
                    Layout = new LinearLayout
                    {
                        LinearOrientation = LinearLayout.Orientation.Horizontal,
                        CellPadding = new Size2D(Spacing, 0)
                    }
                };

                var row = new List<View>();

                int rowNumber = _grid.Count;
                for (int i = 0; i < _moviesPerRow && _nextMovieIndexToBuild < _movies.Count; i++, _nextMovieIndexToBuild++)
                {
                    var card = CreatePosterCard(_movies[_nextMovieIndexToBuild], rowNumber);
                    row.Add(card);
                    rowContainer.Add(card);
                }

                viewport.Add(rowContainer);
                _verticalContainer.Add(viewport);

                _grid.Add(row);
                _rowContainers.Add(rowContainer);
                _viewports.Add(viewport);

                _nextRowY += rowHeight;
                builtRows++;
            }

            if (_nextMovieIndexToBuild >= _movies.Count)
            {
                _isGridBuildCompleted = true;
                _buildTimer?.Stop();
            }
        }

        public override void OnShow()
        {
            if (_movies == null || _movies.Count == 0)
                return;

            _allowHighQualityUpgrade = false;
            if (_grid.Count == 0)
            {
                BuildGridBatch(1);
            }
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
            ResetSettingsPanelVisualState();

            Highlight(true);
            FocusManager.Instance.SetCurrentFocusView(_grid[_rowIndex][_colIndex]);
            ScrollHorizontalIfNeeded();
            ScrollVerticalIfNeeded();
            EnsureVisiblePostersLoaded();
            StartBuildTimer();
            StartPosterRefreshTimer();
            StartHighQualityDelayTimer();
        }

        public override void OnHide()
        {
            UiAnimator.StopAndDispose(ref _horizontalScrollAnimation);
            UiAnimator.StopAndDispose(ref _verticalScrollAnimation);
            UiAnimator.StopAndDisposeAll(_focusAnimations);
            _buildTimer?.Stop();
            _posterRefreshTimer?.Stop();
            _highQualityDelayTimer?.Stop();
        }

        private void ResetSettingsPanelVisualState()
        {
            if (_settingsPanel == null)
            {
                return;
            }

            _settingsPanel.PositionX = _settingsPanelBaseX;
            _settingsPanel.Opacity = 1.0f;
            _settingsPanel.Hide();
        }

        private int CalculateColumns()
        {
            var screenWidth = Window.Default.Size.Width;
            var usable = screenWidth - (SidePadding * 2);
            return Math.Max(1, usable / (PosterWidth + Spacing));
        }

        private View CreatePosterCard(JellyfinMovie movie, int rowNumber)
        {
            BuildPosterUrls(movie, out var posterLowUrl, out var posterHighUrl);

            var wrapper = MediaCardFactory.CreateImageCard(
                PosterWidth,
                PosterHeight,
                CardTextHeight,
                movie.Name,
                subtitle: null,
                imageUrl: null,
                out var poster,
                focusBorder: FocusBorder,
                titlePoint: (int)UiTheme.MediaCardTitle,
                subtitlePoint: (int)UiTheme.MediaCardSubtitle
            );

            _posterStates[poster] = new PosterLoadState
            {
                LowUrl = posterLowUrl,
                HighUrl = posterHighUrl,
                Row = rowNumber,
                Quality = PosterQuality.Unloaded
            };

            return wrapper;
        }

        private void BuildPosterUrls(JellyfinMovie movie, out string lowUrl, out string highUrl)
        {
            lowUrl = null;
            highUrl = null;

            var apiKey = Uri.EscapeDataString(AppState.AccessToken);
            var serverUrl = AppState.Jellyfin.ServerUrl;

            string basePath = null;
            int highQuality = 90;
            if (movie.HasPrimary)
            {
                basePath = $"{serverUrl}/Items/{movie.Id}/Images/Primary/0";
            }
            else if (movie.HasThumb)
            {
                basePath = $"{serverUrl}/Items/{movie.Id}/Images/Thumb/0";
            }
            else if (movie.HasBackdrop)
            {
                basePath = $"{serverUrl}/Items/{movie.Id}/Images/Backdrop/0";
                highQuality = 85;
            }

            if (string.IsNullOrWhiteSpace(basePath))
                return;

            int lowWidth = Math.Max(120, PosterWidth / 2);
            lowUrl = $"{basePath}?maxWidth={lowWidth}&quality=52&api_key={apiKey}";
            highUrl = $"{basePath}?maxWidth={PosterWidth}&quality={highQuality}&api_key={apiKey}";
        }

        private void StartBuildTimer()
        {
            if (_isGridBuildCompleted)
                return;

            _buildTimer ??= new Timer(BuildTickMs);
            _buildTimer.Stop();
            _buildTimer.Tick -= OnBuildTimerTick;
            _buildTimer.Tick += OnBuildTimerTick;
            _buildTimer.Start();
        }

        private bool OnBuildTimerTick(object sender, Timer.TickEventArgs e)
        {
            BuildGridBatch(RowBuildBatchSize);
            EnsureVisiblePostersLoaded();

            if (_isGridBuildCompleted)
            {
                _buildTimer?.Stop();
                return false;
            }

            return true;
        }

        private void StartPosterRefreshTimer()
        {
            _posterRefreshTimer ??= new Timer(PosterRefreshIntervalMs);
            _posterRefreshTimer.Stop();
            _posterRefreshTimer.Tick -= OnPosterRefreshTick;
            _posterRefreshTimer.Tick += OnPosterRefreshTick;
            _posterRefreshTimer.Start();
        }

        private bool OnPosterRefreshTick(object sender, Timer.TickEventArgs e)
        {
            EnsureVisiblePostersLoaded();
            return true;
        }

        private void StartHighQualityDelayTimer()
        {
            _highQualityDelayTimer ??= new Timer(HighQualityDelayMs);
            _highQualityDelayTimer.Stop();
            _highQualityDelayTimer.Tick -= OnHighQualityDelayTick;
            _highQualityDelayTimer.Tick += OnHighQualityDelayTick;
            _highQualityDelayTimer.Start();
        }

        private bool OnHighQualityDelayTick(object sender, Timer.TickEventArgs e)
        {
            _highQualityDelayTimer?.Stop();
            _allowHighQualityUpgrade = true;
            EnsureVisiblePostersLoaded();
            return false;
        }

        private void EnsureVisiblePostersLoaded()
        {
            if (_posterStates.Count == 0 || _grid.Count == 0)
                return;

            int rowHeight = (PosterHeight + CardTextHeight) + (FocusPad * 2) + RowSpacing;
            int viewportHeight = _contentViewport != null && _contentViewport.SizeHeight > 0
                ? (int)_contentViewport.SizeHeight
                : Math.Max(0, Window.Default.Size.Height - ContentViewportStartY);
            int visibleTop = (int)(-_verticalContainer.PositionY);
            int visibleBottom = visibleTop + viewportHeight;

            int firstVisibleRow = Math.Max(0, (visibleTop / rowHeight) - PosterVisibleRowBuffer);
            int lastVisibleRow = Math.Min(_grid.Count - 1, (visibleBottom / rowHeight) + PosterVisibleRowBuffer);

            int keepLowMin = Math.Max(0, firstVisibleRow - PosterKeepLowRowBuffer);
            int keepLowMax = Math.Min(_grid.Count - 1, lastVisibleRow + PosterKeepLowRowBuffer);

            foreach (var pair in _posterStates)
            {
                var image = pair.Key;
                var state = pair.Value;
                bool isVisibleRange = state.Row >= firstVisibleRow && state.Row <= lastVisibleRow;
                bool keepLowRange = state.Row >= keepLowMin && state.Row <= keepLowMax;

                if (isVisibleRange)
                {
                    if (state.Quality == PosterQuality.Unloaded && !string.IsNullOrWhiteSpace(state.LowUrl))
                    {
                        image.ResourceUrl = state.LowUrl;
                        state.Quality = PosterQuality.Low;
                    }

                    if (_allowHighQualityUpgrade &&
                        state.Quality == PosterQuality.Low &&
                        !string.IsNullOrWhiteSpace(state.HighUrl))
                    {
                        image.ResourceUrl = state.HighUrl;
                        state.Quality = PosterQuality.High;
                    }
                }
                else if (keepLowRange)
                {
                    if (state.Quality == PosterQuality.High && !string.IsNullOrWhiteSpace(state.LowUrl))
                    {
                        image.ResourceUrl = state.LowUrl;
                        state.Quality = PosterQuality.Low;
                    }
                    else if (state.Quality == PosterQuality.Unloaded && !string.IsNullOrWhiteSpace(state.LowUrl))
                    {
                        image.ResourceUrl = state.LowUrl;
                        state.Quality = PosterQuality.Low;
                    }
                }
                else if (state.Quality != PosterQuality.Unloaded)
                {
                    image.ResourceUrl = null;
                    state.Quality = PosterQuality.Unloaded;
                }
            }
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

            if (_grid.Count == 0)
            {
                if (key == AppKey.Back)
                    NavigationService.NavigateBack();
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
            if (_grid.Count == 0 || _grid[0].Count == 0)
                return;

            Highlight(false);

            _rowIndex = Math.Clamp(_rowIndex + rowDelta, 0, _grid.Count - 1);
            _colIndex = Math.Clamp(_colIndex + colDelta, 0, _grid[_rowIndex].Count - 1);

            Highlight(true);
            FocusManager.Instance.SetCurrentFocusView(_grid[_rowIndex][_colIndex]);

            ScrollHorizontalIfNeeded();
            ScrollVerticalIfNeeded();
            EnsureVisiblePostersLoaded();
        }

        private void Highlight(bool focused)
        {
            var card = _grid[_rowIndex][_colIndex];
            var frame = MediaCardFocus.GetCardFrame(card);
            var content = MediaCardFocus.GetCardContent(card);
            if (content != null)
                AnimateCardScale(content, focused ? new Vector3(FocusScale, FocusScale, 1f) : Vector3.One);

            card.Scale = Vector3.One;
            card.PositionZ = focused ? 20 : 0;

            if (focused)
                MediaCardFocus.ApplyFrameFocus(frame, _focusBorderColor, _focusColor, UseLightweightFocusMode);
            else
                MediaCardFocus.ClearFrameFocus(frame);
        }

        private void AnimateCardScale(View content, Vector3 targetScale)
        {
            if (content == null)
            {
                return;
            }

            if (UseLightweightFocusMode)
            {
                if (_focusAnimations.TryGetValue(content, out var existingDirect))
                {
                    UiAnimator.StopAndDispose(ref existingDirect);
                    _focusAnimations.Remove(content);
                }

                content.Scale = targetScale;
                return;
            }

            if (_focusAnimations.TryGetValue(content, out var existing))
            {
                UiAnimator.StopAndDispose(ref existing);
                _focusAnimations.Remove(content);
            }

            var animation = UiAnimator.Start(
                UiAnimator.FocusDurationMs,
                anim => anim.AnimateTo(content, "Scale", targetScale),
                () => _focusAnimations.Remove(content)
            );

            _focusAnimations[content] = animation;
        }

        private void FocusSettings(bool focused)
        {
            _settingsFocused = focused;
            _settingsButton.Scale = focused ? new Vector3(1.1f, 1.1f, 1f) : Vector3.One;

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
            _settingsPanelBaseX = Window.Default.Size.Width - 520;
            _settingsPanel = MediaBrowserChrome.CreateSettingsPanel(_settingsPanelBaseX, TopBarHeight + 16, TopBarZ + 5);

            _settingsPanel.Opacity = 1.0f;
            _settingsPanel.Hide();
            var title = MediaBrowserChrome.CreateSettingsPanelTitle();
            var list = MediaBrowserChrome.CreateSettingsOptionsList();

            _settingsOptions.Clear();
            _settingsOptions.Add(MediaBrowserChrome.CreateSettingsOption("Playback Settings"));
            _settingsOptions.Add(MediaBrowserChrome.CreateSettingsOption("Logout"));
            _settingsOptions.Add(MediaBrowserChrome.CreateSettingsOption("Switch Server"));

            foreach (var opt in _settingsOptions)
                list.Add(opt);

            _settingsPanel.Add(title);
            _settingsPanel.Add(list);

            Add(_settingsPanel);
        }

        private void ShowSettingsPanel()
        {
            _settingsVisible = true;
            _settingsIndex = 0;
            UpdateSettingsHighlight();

            _settingsPanel.PositionX = _settingsPanelBaseX;
            _settingsPanel.Opacity = 1.0f;
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
            MediaBrowserChrome.UpdateSettingsHighlight(_settingsOptions, _settingsIndex);
        }

        private void ActivateSettingsOption()
        {
            if (_settingsIndex == 0)
            {
                HideSettingsPanel();
                NavigationService.NavigateWithLoading(
                    () => new SettingsScreen(),
                    "Loading settings..."
                );
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
            NavigationService.NavigateWithLoading(
                () => new ServerSetupScreen(),
                "Loading server setup...",
                addToStack: false
            );
        }

        private void ScrollHorizontalIfNeeded()
        {
            var rowContainer = _rowContainers[_rowIndex];
            var viewport = _viewports[_rowIndex];
            var focused = _grid[_rowIndex][_colIndex];
            var targetX = rowContainer.PositionX;

            if (_colIndex == 0)
            {
                targetX = SidePadding;
            }
            else
            {
                var offset = -rowContainer.PositionX;
                var viewportWidth = viewport.SizeWidth;

                var left = focused.PositionX;
                var right = left + PosterWidth;

                var visibleLeft = offset;
                var visibleRight = offset + viewportWidth;

                if (right > visibleRight)
                    targetX -= (right - visibleRight + Spacing);
                else if (left < visibleLeft)
                    targetX += (visibleLeft - left + Spacing);
            }

            if (Math.Abs(targetX - rowContainer.PositionX) < 0.5f)
            {
                return;
            }

            if (UseLightweightFocusMode)
            {
                UiAnimator.StopAndDispose(ref _horizontalScrollAnimation);
                rowContainer.PositionX = targetX;
                return;
            }

            UiAnimator.Replace(
                ref _horizontalScrollAnimation,
                UiAnimator.AnimateTo(rowContainer, "PositionX", targetX, UiAnimator.ScrollDurationMs)
            );
            EnsureVisiblePostersLoaded();
        }

        private void ScrollVerticalIfNeeded()
        {
            var viewportHeight = _contentViewport != null && _contentViewport.SizeHeight > 0
                ? _contentViewport.SizeHeight
                : Math.Max(0, Window.Default.Size.Height - ContentViewportStartY);
            var rowHeight = (PosterHeight + CardTextHeight) + (FocusPad * 2) + RowSpacing;

            var currentOffset = -_verticalContainer.PositionY;
            var targetY = _verticalContainer.PositionY;

            var rowTop = _rowIndex * rowHeight;
            var rowBottom = rowTop + rowHeight;

            var visibleTop = currentOffset;
            var visibleBottom = currentOffset + viewportHeight - 200;

            // Scroll down only when row hits bottom edge
            if (rowBottom > visibleBottom)
            {
                var delta = rowBottom - visibleBottom;
                targetY -= delta;
            }
            // Scroll up only when row hits top edge
            else if (rowTop < visibleTop)
            {
                var delta = visibleTop - rowTop;
                targetY += delta;
            }

            if (targetY > 0)
                targetY = 0;

            if (Math.Abs(targetY - _verticalContainer.PositionY) < 0.5f)
            {
                return;
            }

            if (UseLightweightFocusMode)
            {
                UiAnimator.StopAndDispose(ref _verticalScrollAnimation);
                _verticalContainer.PositionY = targetY;
                return;
            }

            UiAnimator.Replace(
                ref _verticalScrollAnimation,
                UiAnimator.AnimateTo(_verticalContainer, "PositionY", targetY, UiAnimator.ScrollDurationMs)
            );
            EnsureVisiblePostersLoaded();
        }



        private void OpenMovie()
        {
            var index = (_rowIndex * _moviesPerRow) + _colIndex;
            if (index >= _movies.Count)
                return;

            var movie = _movies[index];

            if (movie.ItemType == "Series")
            {
                NavigationService.NavigateWithLoading(
                    () => new SeriesDetailsScreen(movie),
                    "Loading details..."
                );
            }
            else if (movie.ItemType == "Episode")
            {
                NavigationService.NavigateWithLoading(
                    () => new EpisodeDetailsLoadingScreen(movie),
                    "Loading details..."
                );
            }
            else // Movie
            {
                NavigationService.NavigateWithLoading(
                    () => new MovieDetailsScreen(movie),
                    "Loading details..."
                );
            }
        }

    }
}

