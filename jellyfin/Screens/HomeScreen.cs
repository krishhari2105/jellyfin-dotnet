using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Core;
using JellyfinTizen.Models;
using JellyfinTizen.UI;

namespace JellyfinTizen.Screens
{
    public class HomeScreen : ScreenBase, IKeyHandler
    {
        private const int TopBarHeight = UiTheme.HomeTopBarHeight;
        private const int SidePadding = UiTheme.HomeSidePadding;
        private const int FocusBorder = UiTheme.HomeFocusBorder;
        private const int FocusPad = UiTheme.HomeFocusPad;
        private const int ContentViewportTopInset = 4;
        private const int ContentViewportStartY = TopBarHeight + ContentViewportTopInset;
        private const float FocusScale = UiTheme.MediaCardFocusScale;

        private readonly List<HomeRowData> _rows;
        private readonly List<List<View>> _rowCards = new();
        private readonly List<View> _rowContainers = new();
        private readonly List<View> _rowViewports = new();
        private readonly List<View> _rowBlocks = new();
        private readonly List<int> _rowCardWidths = new();
        private readonly List<int> _rowSpacings = new();
        private readonly List<int> _rowTops = new();
        private readonly List<int> _rowHeights = new();

        private View _contentViewport;
        private View _verticalContainer;
        private int _rowIndex;
        private int _colIndex;
        private bool _focusInitialized;

        private bool _settingsFocused;
        private View _settingsButton;
        private View _settingsPanel;
        private readonly List<View> _settingsOptions = new();
        private int _settingsIndex;
        private bool _settingsVisible;
        private int _settingsPanelBaseX;
        private bool _libraryNavigationInProgress;

        private readonly Color _focusColor = UiTheme.MediaCardFocusBorder;

        public HomeScreen(List<HomeRowData> rows)
        {
            _rows = rows ?? new List<HomeRowData>();

            var root = UiFactory.CreateAtmosphericBackground();

            var topBar = MediaBrowserChrome.CreateTopBar(
                "Jellyfin",
                TopBarHeight,
                SidePadding,
                SidePadding,
                positionZ: 10,
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
                PositionY = 0,
                PositionZ = 0
            };

            _contentViewport.Add(_verticalContainer);
            root.Add(_contentViewport);
            root.Add(topBar);

            BuildRows();
            Add(root);
            CreateSettingsPanel();
        }

        public override void OnShow()
        {
            // Litefin-style "server is truth": always re-fetch the Continue Watching row fresh
            // from the server on every OnShow. HomeScreen is a long-lived, reused instance, so
            // this runs each time Home becomes active again, not just on first construction.
            _ = RefreshContinueWatchingFromServerAsync();

            if (_rows.Count == 0 || _rowCards.Count == 0 || _rowCards[0].Count == 0)
                return;

            if (!_focusInitialized)
            {
                _rowIndex = 0;
                _colIndex = 0;
                _focusInitialized = true;
            }

            _settingsFocused = false;
            _settingsVisible = false;
            _libraryNavigationInProgress = false;
            ResetSettingsPanelVisualState();

            Highlight(true);
            ScrollHorizontalIfNeeded();
            ScrollVerticalIfNeeded();
            try
            {
                Tizen.Applications.CoreApplication.Post(() =>
                {
                    FocusManager.Instance.SetCurrentFocusView(_rowCards[_rowIndex][_colIndex]);
                    ScrollHorizontalIfNeeded();
                    ScrollVerticalIfNeeded();
                });
            }
            catch
            {
                FocusManager.Instance.SetCurrentFocusView(_rowCards[_rowIndex][_colIndex]);
            }
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

        private void BuildRows()
        {
            int y = 0;

            foreach (var row in _rows)
            {
                if (row.Items == null || row.Items.Count == 0)
                    continue;

                var built = BuildSingleRowBlock(row, y);

                _verticalContainer.Add(built.RowBlock);

                _rowCards.Add(built.Cards);
                _rowContainers.Add(built.RowContainer);
                _rowViewports.Add(built.Viewport);
                _rowBlocks.Add(built.RowBlock);
                _rowCardWidths.Add(built.CardWidth);
                _rowSpacings.Add(built.Spacing);
                _rowTops.Add(y);
                _rowHeights.Add(built.TotalHeight);

                y += built.TotalHeight + built.RowSpacingAfter;
            }
        }

        private readonly struct BuiltRowBlock
        {
            public View RowBlock { get; init; }
            public View Viewport { get; init; }
            public View RowContainer { get; init; }
            public List<View> Cards { get; init; }
            public int CardWidth { get; init; }
            public int Spacing { get; init; }
            public int TotalHeight { get; init; }
            public int RowSpacingAfter { get; init; }
        }

        // Builds the full visual block (title + viewport + card row) for a single HomeRowData,
        // identical to the per-row construction inside BuildRows(). Extracted so the same
        // logic can be reused when a Continue Watching row needs to be created from scratch
        // after being previously removed (local-only optimistic insert), instead of
        // duplicating this layout code.
        private BuiltRowBlock BuildSingleRowBlock(HomeRowData row, int y)
        {
            var rowInfo = GetRowStyle(row.Kind);
            var textHeight = GetCardTextHeight(row, rowInfo.CardWidth);
            var cardHeight = rowInfo.CardHeight + textHeight;
            var rowHeight = rowInfo.RowHeight + textHeight;
            var titleImageGap = 0;

            var viewportTopPadding = (ushort)Math.Min(FocusPad + titleImageGap, (int)ushort.MaxValue);
            var viewportBottomPadding = (ushort)Math.Max(0, FocusPad - titleImageGap);
            var rowBlock = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = rowHeight + (FocusPad * 2),
                PositionY = y
            };

            var title = MediaBrowserChrome.CreateRowTitle(row.Title, SidePadding);
            title.HeightSpecification = rowInfo.TitleHeight;

            var viewport = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = cardHeight + (FocusPad * 2),
                PositionY = rowInfo.TitleHeight + 10,
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
                    CellPadding = new Size2D(rowInfo.Spacing, 0)
                }
            };

            var cards = new List<View>();

            foreach (var item in row.Items)
            {
                var card = CreateCard(row.Kind, item, rowInfo.CardWidth, rowInfo.CardHeight, textHeight);
                cards.Add(card);
                rowContainer.Add(card);
            }

            viewport.Add(rowContainer);
            rowBlock.Add(title);
            rowBlock.Add(viewport);

            return new BuiltRowBlock
            {
                RowBlock = rowBlock,
                Viewport = viewport,
                RowContainer = rowContainer,
                Cards = cards,
                CardWidth = rowInfo.CardWidth,
                Spacing = rowInfo.Spacing,
                TotalHeight = rowHeight + (FocusPad * 2),
                RowSpacingAfter = rowInfo.RowSpacing
            };
        }

        private int GetCardTextHeight(HomeRowData row, int cardWidth)
        {
            int preferredTextHeight = GetPreferredCardTextHeight(row);
            if (row?.Items == null || row.Items.Count == 0)
                return preferredTextHeight;

            int maxTextHeight = preferredTextHeight;
            bool allowSubtitle = row.Kind != HomeRowKind.Libraries;
            foreach (var item in row.Items)
            {
                if (item == null)
                    continue;

                var subtitle = allowSubtitle ? item.Subtitle : null;
                maxTextHeight = Math.Max(
                    maxTextHeight,
                    MediaCardFactory.GetRecommendedTextHeight(
                        cardWidth,
                        preferredTextHeight,
                        item.Title,
                        subtitle,
                        (int)UiTheme.MediaCardTitle,
                        (int)UiTheme.MediaCardSubtitle));
            }

            return maxTextHeight;
        }

        private static int GetPreferredCardTextHeight(HomeRowData row)
        {
            bool hasSubtitle = row?.Kind != HomeRowKind.Libraries &&
                               row?.Items != null &&
                               row.Items.Exists(item => !string.IsNullOrWhiteSpace(item?.Subtitle));

            if (!hasSubtitle)
            {
                return row?.Kind switch
                {
                    HomeRowKind.Libraries => 72,
                    _ => 90
                };
            }

            return row.Kind switch
            {
                HomeRowKind.NextUp => 108,
                HomeRowKind.ContinueWatching => 108,
                _ => 104
            };
        }

        private (int CardWidth, int CardHeight, int Spacing, int TitleHeight, int RowHeight, int RowSpacing) GetRowStyle(HomeRowKind kind)
        {
            return kind switch
            {
                HomeRowKind.Libraries => (420, 236, 30, UiTheme.MediaRowTitleHeight, 316, 24),
                HomeRowKind.NextUp => (420, 236, 30, UiTheme.MediaRowTitleHeight, 316, 24),
                HomeRowKind.ContinueWatching => (420, 236, 30, UiTheme.MediaRowTitleHeight, 316, 24),
                _ => (260, 390, 24, UiTheme.MediaRowTitleHeight, 460, 26)
            };
        }

        private View CreateCard(HomeRowKind kind, HomeItemData item, int width, int imageHeight, int textHeight)
        {
            var hasSubtitle = !string.IsNullOrWhiteSpace(item.Subtitle) && kind != HomeRowKind.Libraries;
            var progressRatio = kind == HomeRowKind.ContinueWatching
                ? GetPlaybackProgressRatio(item?.Media)
                : null;

            return MediaCardFactory.CreateImageCard(
                width,
                imageHeight,
                textHeight,
                item.Title,
                hasSubtitle ? item.Subtitle : null,
                item.ImageUrl,
                out _,
                focusBorder: FocusBorder,
                titlePoint: (int)UiTheme.MediaCardTitle,
                subtitlePoint: (int)UiTheme.MediaCardSubtitle,
                progressRatio: progressRatio
            );
        }

        private static float? GetPlaybackProgressRatio(JellyfinMovie media)
        {
            if (media == null || media.PlaybackPositionTicks <= 0 || media.RunTimeTicks <= 0)
                return null;

            return (float)Math.Clamp(
                media.PlaybackPositionTicks / (double)media.RunTimeTicks,
                0d,
                1d);
        }

        // Builds the image URL for a Continue Watching card exactly the way HomeLoadingScreen
        // does on a full reload (thumb/backdrop preference order, cached, no network call —
        // it only computes a URL string against already-known HasThumb/HasBackdrop/HasPrimary
        // flags on the JellyfinMovie).
        private static string BuildContinueWatchingImageUrl(JellyfinMovie media)
        {
            var serverUrl = AppState.Jellyfin?.ServerUrl;
            var apiKey = Uri.EscapeDataString(AppState.AccessToken ?? string.Empty);
            if (string.IsNullOrWhiteSpace(serverUrl))
                return null;

            var imageUrl = HomeLoadingScreen.GetThumbOrBackdropUrl(media, serverUrl, apiKey, 280);
            if (string.IsNullOrWhiteSpace(imageUrl))
                return null;

            return HomeLoadingScreen.BuildCachedImageUrl(
                $"continue:{media.Id}:280:70:{apiKey}",
                () => AppState.RewriteImageUrlForTailscale(imageUrl.Replace("quality=82", "quality=70")));
        }

        private int ComputeNextRowY()
        {
            if (_rowTops.Count == 0 || _rowHeights.Count == 0)
                return 0;

            int lastIdx = _rowTops.Count - 1;
            return _rowTops[lastIdx] + _rowHeights[lastIdx];
        }

        // Litefin-style "server is truth": always re-fetch the Continue Watching list fresh
        // from the server (same getResumeItems-equivalent endpoint, GetContinueWatchingAsync)
        // and rebuild the row to match, rather than relying only on the local optimistic
        // update chain. Non-blocking; the UI rebuild is marshalled onto the UI thread.
        private async Task RefreshContinueWatchingFromServerAsync()
        {
            List<JellyfinMovie> serverItems;
            try
            {
                serverItems = await AppState.Jellyfin.GetContinueWatchingAsync(20);
            }
            catch (System.Exception ex)
            {
                TailscaleDebugLog.Add($"[ResumeState] HomeScreen.RefreshContinueWatchingFromServerAsync: fetch failed: {ex.Message}");
                return;
            }

            RunOnUiThread(() =>
            {
                try
                {
                    long topTicks = (serverItems != null && serverItems.Count > 0) ? serverItems[0].PlaybackPositionTicks : 0;
                    TailscaleDebugLog.Add($"[ResumeState] HomeScreen.RefreshContinueWatchingFromServerAsync: server truth ContinueWatching count={serverItems?.Count ?? 0}, topItemTicks={topTicks}");
                    ApplyServerContinueWatching(serverItems);
                }
                catch
                {
                    // Screen may have been navigated away/disposed between fetch and continuation.
                }
            });
        }

        // Reconciles the on-screen Continue Watching row to exactly match the server's list:
        // removes the row if the server returns nothing, inserts a fresh row if the server has
        // items but none is shown, or replaces the row's cards in place (preserving row index /
        // scroll position / focus) when the row already exists. Server value is authoritative.
        private void ApplyServerContinueWatching(List<JellyfinMovie> serverItems)
        {
            int cwIdx = _rows.FindIndex(r => r != null && r.Kind == HomeRowKind.ContinueWatching);

            var items = new List<HomeItemData>();
            if (serverItems != null)
            {
                foreach (var m in serverItems)
                {
                    if (m == null || string.IsNullOrWhiteSpace(m.Id))
                        continue;
                    items.Add(new HomeItemData
                    {
                        Title = m.Name,
                        Subtitle = m.SeriesName,
                        ImageUrl = BuildContinueWatchingImageUrl(m),
                        Media = m
                    });
                }
            }

            if (items.Count == 0)
            {
                if (cwIdx >= 0)
                    RemoveRowAt(cwIdx, wasFocusedRow: cwIdx == _rowIndex);
                return;
            }

            if (cwIdx < 0)
            {
                InsertContinueWatchingRowWithItems(items);
                return;
            }

            // Row exists — replace its cards in place, keeping row index / position / focus stable.
            var row = _rows[cwIdx];
            row.Items = items;

            if (cwIdx >= _rowCards.Count || cwIdx >= _rowContainers.Count)
                return;

            var rowContainer = _rowContainers[cwIdx];
            var oldCards = _rowCards[cwIdx];
            foreach (var c in oldCards)
            {
                rowContainer.Remove(c);
                try { c.Dispose(); } catch { }
            }

            var rowInfo = GetRowStyle(row.Kind);
            var textHeight = GetCardTextHeight(row, rowInfo.CardWidth);
            var newCards = new List<View>();
            foreach (var item in items)
            {
                var card = CreateCard(row.Kind, item, rowInfo.CardWidth, rowInfo.CardHeight, textHeight);
                newCards.Add(card);
                rowContainer.Add(card);
            }
            _rowCards[cwIdx] = newCards;

            if (_rowIndex == cwIdx)
            {
                _colIndex = Math.Clamp(_colIndex, 0, newCards.Count - 1);
                Highlight(true);
                FocusManager.Instance.SetCurrentFocusView(newCards[_colIndex]);
                ScrollHorizontalIfNeeded();
                ScrollVerticalIfNeeded();
            }
        }

        // Inserts a fresh Continue Watching row containing the given items at the canonical
        // position (before Recently Added, else appended). Mirrors the parallel-list
        // bookkeeping used by the single-item optimistic insert, but with a full item list.
        private void InsertContinueWatchingRowWithItems(List<HomeItemData> items)
        {
            var newRow = new HomeRowData
            {
                Title = "Continue Watching",
                Kind = HomeRowKind.ContinueWatching
            };
            newRow.Items.AddRange(items);

            int insertRowIdx = _rows.FindIndex(r => r.Kind == HomeRowKind.RecentlyAdded);
            if (insertRowIdx < 0)
                insertRowIdx = _rows.Count;

            _rows.Insert(insertRowIdx, newRow);

            int y = insertRowIdx < _rowTops.Count ? _rowTops[insertRowIdx] : ComputeNextRowY();
            var built = BuildSingleRowBlock(newRow, y);

            _verticalContainer.Add(built.RowBlock);

            _rowCards.Insert(insertRowIdx, built.Cards);
            _rowContainers.Insert(insertRowIdx, built.RowContainer);
            _rowViewports.Insert(insertRowIdx, built.Viewport);
            _rowBlocks.Insert(insertRowIdx, built.RowBlock);
            _rowCardWidths.Insert(insertRowIdx, built.CardWidth);
            _rowSpacings.Insert(insertRowIdx, built.Spacing);
            _rowTops.Insert(insertRowIdx, y);
            _rowHeights.Insert(insertRowIdx, built.TotalHeight);

            int shiftAmount = built.TotalHeight + built.RowSpacingAfter;
            for (int i = insertRowIdx + 1; i < _rowTops.Count; i++)
            {
                _rowTops[i] += shiftAmount;
                _rowBlocks[i].PositionY = _rowTops[i];
            }

            if (insertRowIdx <= _rowIndex)
                _rowIndex += 1;
            _rowIndex = Math.Clamp(_rowIndex, 0, _rowCards.Count - 1);
            _colIndex = Math.Clamp(_colIndex, 0, _rowCards[_rowIndex].Count - 1);
        }


        // parallel per-row tracking list, then shifts subsequent row blocks' PositionY up to
        // close the gap. _verticalContainer uses HeightResizePolicy.FitToChildren, so removing
        // the block is sufficient for the container itself to shrink.
        private void RemoveRowAt(int rowIdx, bool wasFocusedRow)
        {
            var rowBlock = rowIdx < _rowBlocks.Count ? _rowBlocks[rowIdx] : null;
            int removedHeight = rowIdx < _rowHeights.Count ? _rowHeights[rowIdx] : 0;

            if (rowBlock != null)
            {
                _verticalContainer.Remove(rowBlock);
                try { rowBlock.Dispose(); } catch { }
            }

            _rows.RemoveAt(rowIdx);
            if (rowIdx < _rowCards.Count) _rowCards.RemoveAt(rowIdx);
            if (rowIdx < _rowContainers.Count) _rowContainers.RemoveAt(rowIdx);
            if (rowIdx < _rowViewports.Count) _rowViewports.RemoveAt(rowIdx);
            if (rowIdx < _rowBlocks.Count) _rowBlocks.RemoveAt(rowIdx);
            if (rowIdx < _rowCardWidths.Count) _rowCardWidths.RemoveAt(rowIdx);
            if (rowIdx < _rowSpacings.Count) _rowSpacings.RemoveAt(rowIdx);
            if (rowIdx < _rowTops.Count) _rowTops.RemoveAt(rowIdx);
            if (rowIdx < _rowHeights.Count) _rowHeights.RemoveAt(rowIdx);

            // Shift every subsequent row up by the removed row's height, both in the
            // tracked _rowTops bookkeeping and the actual View PositionY.
            for (int i = rowIdx; i < _rowTops.Count; i++)
            {
                _rowTops[i] -= removedHeight;
                if (i < _rowBlocks.Count)
                    _rowBlocks[i].PositionY = _rowTops[i];
            }

            if (_rowCards.Count == 0)
            {
                // Nothing left to focus.
                _rowIndex = 0;
                _colIndex = 0;
                return;
            }

            if (!wasFocusedRow)
            {
                // A row above the focused one was removed — keep the same logical row focused
                // by shifting the index down; the view itself already moved up to compensate.
                if (rowIdx < _rowIndex)
                    _rowIndex -= 1;
                _rowIndex = Math.Clamp(_rowIndex, 0, _rowCards.Count - 1);
                _colIndex = Math.Clamp(_colIndex, 0, _rowCards[_rowIndex].Count - 1);
                return;
            }

            // The focused row itself was removed entirely — move focus to the row that took
            // its place (same index if one exists, else the previous row), mirroring how
            // initial load simply omits an empty row without special-casing focus.
            _rowIndex = Math.Clamp(rowIdx, 0, _rowCards.Count - 1);
            _colIndex = Math.Clamp(_colIndex, 0, _rowCards[_rowIndex].Count - 1);
            Highlight(true);
            FocusManager.Instance.SetCurrentFocusView(_rowCards[_rowIndex][_colIndex]);
            ScrollHorizontalIfNeeded();
            ScrollVerticalIfNeeded();
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
                case AppKey.Left:
                    Move(0, -1);
                    break;
                case AppKey.Right:
                    Move(0, 1);
                    break;
                case AppKey.Down:
                    Move(1, 0);
                    break;
                case AppKey.Up:
                    if (_rowIndex == 0)
                        FocusSettings(true);
                    else
                        Move(-1, 0);
                    break;
                case AppKey.Enter:
                    ActivateFocusedCard();
                    break;
                case AppKey.Back:
                    NavigationService.NavigateBack();
                    break;
            }
        }

        private void Move(int rowDelta, int colDelta)
        {
            var nextRow = Math.Clamp(_rowIndex + rowDelta, 0, _rowCards.Count - 1);
            var nextCol = Math.Clamp(_colIndex + colDelta, 0, _rowCards[nextRow].Count - 1);
            if (nextRow == _rowIndex && nextCol == _colIndex)
                return;

            Highlight(false);

            _rowIndex = nextRow;
            _colIndex = nextCol;

            Highlight(true);
            FocusManager.Instance.SetCurrentFocusView(_rowCards[_rowIndex][_colIndex]);

            ScrollHorizontalIfNeeded();
            ScrollVerticalIfNeeded();
        }

        private void Highlight(bool focused)
        {
            var card = _rowCards[_rowIndex][_colIndex];
            var frame = MediaCardFocus.GetCardFrame(card);
            var scaleTarget = frame ?? card;
            scaleTarget.Scale = focused ? new Vector3(FocusScale, FocusScale, 1f) : Vector3.One;

            if (focused)
            {
                if (frame != null)
                {
                    frame.PositionZ = 30;
                    card.PositionZ = 0;
                }
                else
                {
                    card.PositionZ = 30;
                }

                MediaCardFocus.ApplyFrameFocus(frame, _focusColor);
            }
            else
            {
                if (frame != null)
                    frame.PositionZ = 0;
                card.PositionZ = 0;

                MediaCardFocus.ClearFrameFocus(frame);
            }
        }

        private void ScrollHorizontalIfNeeded()
        {
            var rowContainer = _rowContainers[_rowIndex];
            var viewport = _rowViewports[_rowIndex];
            var focused = _rowCards[_rowIndex][_colIndex];
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
                var right = left + _rowCardWidths[_rowIndex];

                var visibleLeft = offset;
                var visibleRight = offset + viewportWidth;

                var spacing = _rowSpacings[_rowIndex];

                if (right > visibleRight)
                    targetX -= (right - visibleRight + spacing);
                else if (left < visibleLeft)
                    targetX += (visibleLeft - left + spacing);
            }

            if (Math.Abs(targetX - rowContainer.PositionX) < 0.5f)
            {
                return;
            }

            rowContainer.PositionX = targetX;
        }

        private void ScrollVerticalIfNeeded()
        {
            var viewportHeight = _contentViewport != null && _contentViewport.SizeHeight > 0
                ? _contentViewport.SizeHeight
                : Math.Max(0, Window.Default.Size.Height - ContentViewportStartY);
            var currentOffset = -_verticalContainer.PositionY;
            var targetY = _verticalContainer.PositionY;

            var rowTop = _rowTops[_rowIndex];
            var rowBottom = rowTop + _rowHeights[_rowIndex];

            var visibleTop = currentOffset;
            var visibleBottom = currentOffset + viewportHeight - 200;

            if (rowBottom > visibleBottom)
            {
                var delta = rowBottom - visibleBottom;
                targetY -= delta;
            }
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
        }

        private void ActivateFocusedCard()
        {
            var row = _rows[_rowIndex];
            var item = row.Items[_colIndex];

            if (row.Kind == HomeRowKind.Libraries && item.Library != null)
            {
                FireAndForget(OpenLibraryAsync(item.Library), nameof(OpenLibraryAsync));
                return;
            }

            if (item.Media != null)
            {
                if (item.Media.IsSeries)
                {
                    NavigationService.NavigateWithLoading(
                        () => new SeriesDetailsScreen(item.Media),
                        "Loading details..."
                    );
                }
                else if (item.Media.UsesThumbDetailsLayout)
                {
                    NavigationService.NavigateWithLoading(
                        () => new EpisodeDetailsLoadingScreen(item.Media),
                        "Loading details..."
                    );
                }
                else // Movie
                {
                    NavigationService.NavigateWithLoading(
                        () => new MovieDetailsScreen(item.Media),
                        "Loading details..."
                    );
                }
            }
        }

        private async System.Threading.Tasks.Task OpenLibraryAsync(JellyfinLibrary lib)
        {
            if (lib == null || _libraryNavigationInProgress)
                return;

            _libraryNavigationInProgress = true;
            var loadingShownAt = DateTime.UtcNow;
            try
            {
                RunOnUiThread(() =>
                {
                    NavigationService.Navigate(
                        new LoadingScreen("Loading items...")
                    );
                });

                var items = await AppState.Jellyfin.GetLibraryItemsAsync(lib.Id, lib.LibraryItemTypes);
                var elapsedMs = (DateTime.UtcNow - loadingShownAt).TotalMilliseconds;
                if (elapsedMs < 280)
                {
                    await System.Threading.Tasks.Task.Delay((int)(280 - elapsedMs));
                }

                RunOnUiThread(() =>
                {
                    NavigationService.Navigate(
                        new LibraryMoviesGridScreen(lib, items),
                        addToStack: false
                    );
                });
            }
            catch
            {
                RunOnUiThread(() => NavigationService.NavigateBack());
            }
            finally
            {
                _libraryNavigationInProgress = false;
            }
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
                FocusManager.Instance.SetCurrentFocusView(_rowCards[_rowIndex][_colIndex]);
            }
        }

        private void CreateSettingsPanel()
        {
            _settingsPanelBaseX = Window.Default.Size.Width - 520;
            _settingsPanel = MediaBrowserChrome.CreateSettingsPanel(_settingsPanelBaseX, TopBarHeight + 16);

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
                FireAndForget(LogoutAsync(), nameof(LogoutAsync));
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

            RunOnUiThread(() =>
            {
                NavigationService.Navigate(
                    new LoadingScreen("Fetching users..."),
                    addToStack: false
                );
            });

            try
            {
                var users = await AppState.Jellyfin.GetPublicUsersAsync();
                RunOnUiThread(() =>
                {
                    NavigationService.Navigate(
                        new UserSelectScreen(users),
                        addToStack: false
                    );
                });
            }
            catch
            {
                RunOnUiThread(() =>
                {
                    NavigationService.Navigate(
                        AppState.HasStoredServers()
                            ? new ServerPickerScreen("Failed to fetch users. Please try again.")
                            : new ServerSetupScreen(),
                        addToStack: false
                    );
                });
            }
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
    }
}
