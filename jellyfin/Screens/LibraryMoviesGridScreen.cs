using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        private const int TopBarZ = 10;
        private const int TopBarLeftPadding = UiTheme.HomeSidePadding;
        private const int PortraitCardWidth = 260;
        private const int PortraitCardHeight = 390;
        private const int LandscapeCardWidth = 420;
        private const int LandscapeCardHeight = 236;
        private const int Spacing = UiTheme.LibraryCardSpacing;
        private const int SidePadding = UiTheme.LibrarySidePadding;
        private const int TopBarRightPadding = UiTheme.HomeSidePadding;
        private const int RowSpacing = UiTheme.LibraryRowSpacing;
        private const int FocusBorder = UiTheme.LibraryFocusBorder;
        private const int FocusPad = UiTheme.LibraryFocusPad;
        private const int ContentViewportTopInset = 4;
        private const int ContentViewportStartY = TopBarHeight + ContentViewportTopInset;
        private const int TopGlowPadBoost = UiTheme.LibraryTopGlowPadBoost;
        private const float FocusScale = UiTheme.MediaCardFocusScale;
        private const int PreferredPortraitCardTextHeight = 96;
        private const int PreferredLandscapeCardTextHeight = 80;
        private const int RowBuildBatchSize = 3;
        private const int PosterVisibleRowBuffer = 1;
        private const int PosterKeepLowRowBuffer = 2;
        private const int PosterRefreshIntervalMs = 150;
        private const int PosterRefreshBurstTicks = 6;
        private const int BuildTickMs = 15;

        // Upper bound on synchronous view construction per build tick. Kept below a
        // 60fps frame so D-pad input stays responsive while the grid populates.
        private const int BuildBudgetMs = 8;

        private readonly Color _focusColor = UiTheme.MediaCardFocusBorder;

        private readonly List<JellyfinMovie> _movies;
        private readonly int _cardWidth;
        private readonly int _cardHeight;
        private readonly int _preferredCardTextHeight;
        private readonly bool _useLandscapeCards;
        private readonly int _sidePadding;

        private readonly List<List<View>> _grid = new();
        private readonly List<View> _playedBadges = new();
        private readonly List<View> _rowContainers = new();
        private readonly List<View> _viewports = new();
        private readonly Dictionary<int, List<PosterEntry>> _posterEntriesByRow = new();

        // Rows that currently hold decoded poster textures, plus the last evaluated
        // keep-loaded window. Both exist so EnsureVisiblePostersLoaded can no-op when
        // nothing moved and can unload without scanning the whole grid.
        private readonly HashSet<int> _loadedRows = new();
        private readonly List<int> _rowsToUnload = new();
        private int _loadedWindowMin = -1;
        private int _loadedWindowMax = -1;
        private int _loadedWindowRowCount = -1;
        private bool _loadedWindowValid;

        private View _contentViewport;
        private View _verticalContainer;
        private readonly int _cardTextHeight;

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
        private Timer _buildTimer;
        private Timer _posterRefreshTimer;
        private int _posterRefreshTicksRemaining;
        private int _nextMovieIndexToBuild;        private int _nextRowY;
        private bool _isGridBuildCompleted;

        // Library paging state. _totalItemCount is the server's reported item count, so the
        // grid knows when it has fetched everything.
        private readonly JellyfinLibrary _library;
        private int _totalItemCount;
        private bool _isFetchingPage;
        private bool _isHidden;

        private sealed class PosterLoadState
        {
            // Single display-size URL. Posters are fetched exactly once (no low->high
            // upgrade), which halves the on-demand image resizes a weak server has to do.
            public string Url;
            public bool Loaded;
        }

        private sealed class PosterEntry
        {
            public ImageView Image;
            public PosterLoadState State;
        }

        public LibraryMoviesGridScreen(JellyfinLibrary library, List<JellyfinMovie> movies)
            : this(library, movies, totalItemCount: movies?.Count ?? 0)
        {
        }

        public LibraryMoviesGridScreen(
            JellyfinLibrary library,
            List<JellyfinMovie> movies,
            int totalItemCount)
        {
            // Copied into a mutable list because later pages are appended as the user scrolls.
            _movies = movies as List<JellyfinMovie> ?? new List<JellyfinMovie>();
            _library = library;
            _totalItemCount = Math.Max(totalItemCount, _movies.Count);
            _useLandscapeCards = library?.UsesLandscapeGridCards == true;
            _sidePadding = _useLandscapeCards ? UiTheme.HomeSidePadding : SidePadding;
            _cardWidth = _useLandscapeCards ? LandscapeCardWidth : PortraitCardWidth;
            _cardHeight = _useLandscapeCards ? LandscapeCardHeight : PortraitCardHeight;
            _preferredCardTextHeight = _useLandscapeCards ? PreferredLandscapeCardTextHeight : PreferredPortraitCardTextHeight;
            _cardTextHeight = CalculateCardTextHeight();
            _moviesPerRow = CalculateColumns();

            var root = UiFactory.CreateAtmosphericBackground();

            var topBar = MediaBrowserChrome.CreateTopBar(
                library?.Name ?? string.Empty,
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

            var timer = PerfTrace.Start();
            var cardHeight = _cardHeight + _cardTextHeight;
            var rowHeight = cardHeight + (FocusPad * 2) + RowSpacing;
            int builtRows = 0;

            // Each row materialises a full card tree per item, which comfortably exceeds a
            // 60fps frame budget when several rows are built back to back. The batch is
            // therefore capped by elapsed time as well as row count so remote input keeps
            // being serviced while the grid fills in.
            var budget = System.Diagnostics.Stopwatch.StartNew();

            while (_nextMovieIndexToBuild < _movies.Count && builtRows < rowsToBuild)
            {
                if (builtRows > 0 && budget.ElapsedMilliseconds >= BuildBudgetMs)
                    break;

                var viewportTopPadding = (ushort)Math.Min(FocusPad + TopGlowPadBoost, (int)ushort.MaxValue);
                var viewportBottomPadding = (ushort)Math.Max(0, FocusPad - TopGlowPadBoost);
                var viewport = new View
                {
                    WidthResizePolicy = ResizePolicyType.FillToParent,
                    HeightSpecification = cardHeight + (FocusPad * 2),
                    PositionY = _nextRowY,
                    ClippingMode = ClippingModeType.ClipChildren,
                    Padding = new Extents((ushort)_sidePadding, (ushort)_sidePadding, viewportTopPadding, viewportBottomPadding)
                };

                var rowContainer = new View
                {
                    PositionX = _sidePadding,
                    PositionY = 0,
                    Layout = new LinearLayout
                    {
                        LinearOrientation = LinearLayout.Orientation.Horizontal,
                        CellPadding = new Size2D(Spacing, 0)
                    }
                };

                var row = new List<View>(_moviesPerRow);

                int rowNumber = _grid.Count;
                for (int i = 0; i < _moviesPerRow && _nextMovieIndexToBuild < _movies.Count; i++, _nextMovieIndexToBuild++)
                {
                    var card = CreatePosterCard(_movies[_nextMovieIndexToBuild], rowNumber, out var playedBadge);
                    row.Add(card);
                    _playedBadges.Add(playedBadge);
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
                if (HasMorePages)
                {
                    // Everything fetched so far is on screen. Pause building and pull the
                    // next page; the timer resumes when it arrives.
                    _buildTimer?.Stop();
                    FireAndForget(FetchNextLibraryPageAsync(), nameof(FetchNextLibraryPageAsync));
                }
                else
                {
                    _isGridBuildCompleted = true;
                    _buildTimer?.Stop();
                }
            }

            PerfTrace.End("LibraryMoviesGridScreen.BuildGridBatch", timer);
        }

        /// <summary>
        /// True while the server holds items that have not been fetched yet.
        /// </summary>
        private bool HasMorePages =>
            _library != null &&
            _movies != null &&
            _movies.Count < _totalItemCount;

        /// <summary>
        /// Appends the next page of library items and resumes the incremental row build.
        /// Paging keeps the in-flight JSON payload and the number of live card view trees
        /// bounded without ever hiding items from the user.
        /// </summary>
        private async Task FetchNextLibraryPageAsync()
        {
            if (_isFetchingPage || !HasMorePages)
                return;

            _isFetchingPage = true;
            try
            {
                var (items, totalRecordCount) = await AppState.Jellyfin
                    .GetLibraryItemsPageAsync(
                        _library.Id,
                        _library.LibraryItemTypes,
                        startIndex: _movies.Count,
                        limit: JellyfinService.LibraryItemsPageSize)
                    .ConfigureAwait(false);

                RunOnUiThread(() =>
                {
                    _isFetchingPage = false;

                    // The screen may have been hidden or disposed while this page was in
                    // flight; mutating its view tree now would touch disposed NUI actors.
                    if (_isHidden)
                        return;

                    _totalItemCount = Math.Max(_totalItemCount, totalRecordCount);

                    if (items == null || items.Count == 0)
                    {
                        // Nothing further is coming; treat the grid as complete rather than
                        // retrying the same empty page forever.
                        _totalItemCount = _movies.Count;
                        _isGridBuildCompleted = true;
                        return;
                    }

                    _movies.AddRange(items);
                    StartBuildTimer();
                });
            }
            catch (Exception ex)
            {
                AppDebugLog.Add($"[LibraryPaging] Failed to fetch next page: {ex.Message}");
                RunOnUiThread(() => _isFetchingPage = false);
            }
        }

        public override void OnShow()
        {
            _isHidden = false;

            if (_movies == null || _movies.Count == 0)
                return;

            if (_grid.Count == 0)
            {
                BuildGridBatch(1);
            }
            else
            {
                _ = RefreshPlayedStatesFromServerAsync();
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
            ScrollHorizontalIfNeeded();
            ScrollVerticalIfNeeded();
            EnsureVisiblePostersLoaded();
            StartBuildTimer();
            StartPosterRefreshTimer();

            try
            {
                Tizen.Applications.CoreApplication.Post(() =>
                {
                    FocusManager.Instance.SetCurrentFocusView(_grid[_rowIndex][_colIndex]);
                    ScrollHorizontalIfNeeded();
                    ScrollVerticalIfNeeded();
                });
            }
            catch
            {
                FocusManager.Instance.SetCurrentFocusView(_grid[_rowIndex][_colIndex]);
            }
        }

        private async Task RefreshPlayedStatesFromServerAsync()
        {
            if (_movies == null || _movies.Count == 0 || _nextMovieIndexToBuild <= 0)
                return;

            try
            {
                var builtMovieIds = new List<string>();
                int countToRefresh = Math.Min(_nextMovieIndexToBuild, _movies.Count);
                for (int i = 0; i < countToRefresh; i++)
                {
                    var m = _movies[i];
                    if (m != null && !string.IsNullOrWhiteSpace(m.Id))
                    {
                        builtMovieIds.Add(m.Id);
                    }
                }

                if (builtMovieIds.Count == 0)
                    return;

                var serverMovies = await AppState.Jellyfin.GetItemsByIdsAsync(builtMovieIds);
                if (serverMovies == null || serverMovies.Count == 0)
                    return;

                var serverMoviesById = new Dictionary<string, JellyfinMovie>(StringComparer.Ordinal);
                foreach (var serverMovie in serverMovies)
                {
                    if (serverMovie != null && !string.IsNullOrWhiteSpace(serverMovie.Id))
                        serverMoviesById[serverMovie.Id] = serverMovie;
                }

                RunOnUiThread(() =>
                {
                    try
                    {
                        for (int i = 0; i < countToRefresh; i++)
                        {
                            var movie = _movies[i];
                            if (movie == null || string.IsNullOrWhiteSpace(movie.Id))
                                continue;

                            if (serverMoviesById.TryGetValue(movie.Id, out var serverMovie))
                            {
                                movie.Played = serverMovie.Played;
                                if (i < _playedBadges.Count && _playedBadges[i] != null)
                                {
                                    if (movie.Played)
                                        _playedBadges[i].Show();
                                    else
                                        _playedBadges[i].Hide();
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AppDebugLog.Add($"[PlayedState] LibraryMoviesGridScreen.RefreshPlayedStatesFromServerAsync UI update error: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                AppDebugLog.Add($"[PlayedState] LibraryMoviesGridScreen.RefreshPlayedStatesFromServerAsync fetch error: {ex.Message}");
            }
        }

        public override void OnHide()
        {
            // Marks in-flight page results as stale. The screen may be disposed immediately
            // after this (navigate-back), so a late continuation must not touch its views.
            _isHidden = true;

            _buildTimer?.Stop();
            _posterRefreshTimer?.Stop();
            _posterRefreshTicksRemaining = 0;
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
            var usable = screenWidth - (_sidePadding * 2);
            return Math.Max(1, usable / (_cardWidth + Spacing));
        }

        private int CalculateCardTextHeight()
        {
            if (_movies == null || _movies.Count == 0)
                return _preferredCardTextHeight;

            int maxTextHeight = _preferredCardTextHeight;

            // Measured from the items loaded at construction time (the first page). Every
            // card in the grid must share one text height or row positions would drift, so
            // this is not recomputed when later pages arrive. An unusually long title on a
            // later page therefore ellipsizes rather than growing the row; card titles set
            // Ellipsis = true precisely for that case.
            foreach (var movie in _movies)
            {
                if (movie == null)
                    continue;

                maxTextHeight = Math.Max(
                    maxTextHeight,
                    MediaCardFactory.GetRecommendedTextHeight(
                        _cardWidth,
                        _preferredCardTextHeight,
                        movie.Name,
                        null,
                        (int)UiTheme.MediaCardTitle,
                        (int)UiTheme.MediaCardSubtitle));
            }

            return maxTextHeight;
        }

        private View CreatePosterCard(JellyfinMovie movie, int rowNumber, out View playedBadge)
        {
            BuildPosterUrl(movie, out var posterUrl);

            var wrapper = MediaCardFactory.CreateImageCard(
                _cardWidth,
                _cardHeight,
                _cardTextHeight,
                movie.Name,
                subtitle: null,
                imageUrl: null,
                out var poster,
                out playedBadge,
                focusBorder: FocusBorder,
                titlePoint: (int)UiTheme.MediaCardTitle,
                subtitlePoint: (int)UiTheme.MediaCardSubtitle,
                played: movie.Played
            );

            var state = new PosterLoadState
            {
                Url = posterUrl,
                Loaded = false
            };

            if (!_posterEntriesByRow.TryGetValue(rowNumber, out var rowEntries))
            {
                rowEntries = new List<PosterEntry>(_moviesPerRow);
                _posterEntriesByRow[rowNumber] = rowEntries;
            }

            rowEntries.Add(new PosterEntry
            {
                Image = poster,
                State = state
            });

            return wrapper;
        }

        private void BuildPosterUrl(JellyfinMovie movie, out string url)
        {
            url = null;

            var apiKey = Uri.EscapeDataString(AppState.AccessToken);
            var serverUrl = AppState.Jellyfin.ServerUrl;

            string basePath = null;
            if (_useLandscapeCards && movie.HasThumb)
            {
                basePath = $"{serverUrl}/Items/{movie.Id}/Images/Thumb/0";
            }
            else if (_useLandscapeCards && movie.HasBackdrop)
            {
                basePath = $"{serverUrl}/Items/{movie.Id}/Images/Backdrop/0";
            }
            else if (movie.HasPrimary)
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
            }

            if (string.IsNullOrWhiteSpace(basePath))
                return;

            // Single fetch at the card's display size. We no longer request a low-quality
            // placeholder first and upgrade later: on a weak server every distinct
            // maxWidth/quality triggers a separate on-demand resize, so the old low->high
            // scheme doubled the server's image work. quality=50 at the exact card width
            // favours performance over fidelity per project preference.
            url = $"{basePath}?maxWidth={_cardWidth}&quality=50&api_key={apiKey}";
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

        private void StartPosterRefreshTimer(int burstTicks = PosterRefreshBurstTicks)
        {
            _posterRefreshTicksRemaining = Math.Max(_posterRefreshTicksRemaining, Math.Max(1, burstTicks));
            _posterRefreshTimer ??= new Timer(PosterRefreshIntervalMs);
            _posterRefreshTimer.Stop();
            _posterRefreshTimer.Tick -= OnPosterRefreshTick;
            _posterRefreshTimer.Tick += OnPosterRefreshTick;
            _posterRefreshTimer.Start();
        }

        private bool OnPosterRefreshTick(object sender, Timer.TickEventArgs e)
        {
            EnsureVisiblePostersLoaded();

            _posterRefreshTicksRemaining--;
            if (_posterRefreshTicksRemaining <= 0)
            {
                _posterRefreshTimer?.Stop();
                return false;
            }

            return true;
        }

        public override void ReleaseOffscreenResources()
        {
            if (_loadedRows.Count == 0)
            {
                _loadedWindowValid = false;
                return;
            }

            _rowsToUnload.Clear();
            foreach (var row in _loadedRows)
                _rowsToUnload.Add(row);

            for (int i = 0; i < _rowsToUnload.Count; i++)
                UnloadRow(_rowsToUnload[i]);

            _rowsToUnload.Clear();

            // Force the next visibility pass to reload the window from scratch.
            _loadedWindowValid = false;
        }

        public override void RestoreOffscreenResources()
        {
            _loadedWindowValid = false;
            EnsureVisiblePostersLoaded();
        }

        private void EnsureVisiblePostersLoaded()
        {
            if (_posterEntriesByRow.Count == 0 || _grid.Count == 0)
                return;

            var timer = PerfTrace.Start();

            int rowHeight = (_cardHeight + _cardTextHeight) + (FocusPad * 2) + RowSpacing;
            int viewportHeight = _contentViewport != null && _contentViewport.SizeHeight > 0
                ? (int)_contentViewport.SizeHeight
                : Math.Max(0, Window.Default.Size.Height - ContentViewportStartY);
            int visibleTop = (int)(-_verticalContainer.PositionY);
            int visibleBottom = visibleTop + viewportHeight;

            int firstVisibleRow = Math.Max(0, (visibleTop / rowHeight) - PosterVisibleRowBuffer);
            int lastVisibleRow = Math.Min(_grid.Count - 1, (visibleBottom / rowHeight) + PosterVisibleRowBuffer);

            // Keep a small buffer of off-screen rows loaded so scrolling back is instant;
            // unload everything beyond it to cap memory. Posters are now a single quality
            // tier, so "keep loaded" and "visible" are the same operation.
            int keepLoadedMin = Math.Max(0, firstVisibleRow - PosterKeepLowRowBuffer);
            int keepLoadedMax = Math.Min(_grid.Count - 1, lastVisibleRow + PosterKeepLowRowBuffer);

            // This runs on every build tick (15ms) and during scroll bursts. Re-walking
            // every poster entry each time is pure overhead when the window has not moved,
            // so the pass is skipped unless the window or the row count changed.
            if (_loadedWindowValid &&
                _loadedWindowMin == keepLoadedMin &&
                _loadedWindowMax == keepLoadedMax &&
                _loadedWindowRowCount == _grid.Count)
            {
                PerfTrace.End("LibraryMoviesGridScreen.EnsureVisiblePostersLoaded", timer);
                return;
            }

            for (int row = keepLoadedMin; row <= keepLoadedMax; row++)
            {
                EnsureRowLoaded(row);
            }

            // Only rows recorded as loaded need unloading, which avoids touching the
            // entries of every already-unloaded row in a large library.
            if (_loadedRows.Count > 0)
            {
                _rowsToUnload.Clear();
                foreach (var loadedRow in _loadedRows)
                {
                    if (loadedRow < keepLoadedMin || loadedRow > keepLoadedMax)
                        _rowsToUnload.Add(loadedRow);
                }

                for (int i = 0; i < _rowsToUnload.Count; i++)
                {
                    UnloadRow(_rowsToUnload[i]);
                }
            }

            _loadedWindowMin = keepLoadedMin;
            _loadedWindowMax = keepLoadedMax;
            _loadedWindowRowCount = _grid.Count;
            _loadedWindowValid = true;

            PerfTrace.End("LibraryMoviesGridScreen.EnsureVisiblePostersLoaded", timer);
        }

        private void EnsureRowLoaded(int row)
        {
            if (!_posterEntriesByRow.TryGetValue(row, out var entries))
                return;

            bool anyLoaded = false;
            foreach (var entry in entries)
            {
                var state = entry.State;

                if (!state.Loaded && !string.IsNullOrWhiteSpace(state.Url))
                {
                    UiAnimator.FadeInOnImageReady(entry.Image, state.Url);
                    state.Loaded = true;
                }

                if (state.Loaded)
                    anyLoaded = true;
            }

            if (anyLoaded)
                _loadedRows.Add(row);
        }

        private void UnloadRow(int row)
        {
            _loadedRows.Remove(row);

            if (!_posterEntriesByRow.TryGetValue(row, out var entries))
                return;

            foreach (var entry in entries)
            {
                var image = entry.Image;
                var state = entry.State;

                if (!state.Loaded)
                    continue;

                UiAnimator.CancelFadeIn(image);
                image.ResourceUrl = null;
                state.Loaded = false;
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

            var nextRow = Math.Clamp(_rowIndex + rowDelta, 0, _grid.Count - 1);
            var nextCol = Math.Clamp(_colIndex + colDelta, 0, _grid[nextRow].Count - 1);
            if (nextRow == _rowIndex && nextCol == _colIndex)
                return;

            Highlight(false);

            _rowIndex = nextRow;
            _colIndex = nextCol;

            Highlight(true);
            FocusManager.Instance.SetCurrentFocusView(_grid[_rowIndex][_colIndex]);

            ScrollHorizontalIfNeeded();
            ScrollVerticalIfNeeded();
            EnsureVisiblePostersLoaded();
            StartPosterRefreshTimer();
        }

        private void Highlight(bool focused)
        {
            var card = _grid[_rowIndex][_colIndex];
            var frame = MediaCardFocus.GetCardFrame(card);
            var scaleTarget = frame ?? card;
            scaleTarget.Scale = focused ? MediaCardFocus.FocusedScale : MediaCardFocus.UnfocusedScale;
            if (frame != null)
            {
                frame.PositionZ = focused ? 20 : 0;
                card.PositionZ = 0;
            }
            else
            {
                card.PositionZ = focused ? 20 : 0;
            }

            if (focused)
                MediaCardFocus.ApplyFrameFocus(frame, _focusColor);
            else
                MediaCardFocus.ClearFrameFocus(frame);
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
            NavigationService.NavigateWithLoading(
                () => new ServerPickerScreen(),
                "Loading servers...",
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
                targetX = _sidePadding;
            }
            else
            {
                var offset = -rowContainer.PositionX;
                var viewportWidth = viewport.SizeWidth;

                var left = focused.PositionX;
                var right = left + _cardWidth;

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

            rowContainer.PositionX = targetX;
            StartPosterRefreshTimer(1);
        }

        private void ScrollVerticalIfNeeded()
        {
            var viewportHeight = _contentViewport != null && _contentViewport.SizeHeight > 0
                ? _contentViewport.SizeHeight
                : Math.Max(0, Window.Default.Size.Height - ContentViewportStartY);
            var rowHeight = (_cardHeight + _cardTextHeight) + (FocusPad * 2) + RowSpacing;

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

            _verticalContainer.PositionY = targetY;
            StartPosterRefreshTimer(1);
        }



        private void OpenMovie()
        {
            var index = (_rowIndex * _moviesPerRow) + _colIndex;
            if (index >= _movies.Count)
                return;

            var movie = _movies[index];

            if (movie.IsSeries)
            {
                NavigationService.NavigateWithLoading(
                    () => new SeriesDetailsScreen(movie),
                    "Loading details..."
                );
            }
            else if (movie.UsesThumbDetailsLayout)
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

