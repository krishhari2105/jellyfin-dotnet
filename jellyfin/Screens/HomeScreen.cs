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
    public class HomeScreen : ScreenBase, IKeyHandler
    {
        private const int TopBarHeight = UiTheme.HomeTopBarHeight;
        private const int RowsStartY = TopBarHeight + UiTheme.HomeRowsTopGap;
        private const int SidePadding = UiTheme.HomeSidePadding;
        private const int FocusBorder = UiTheme.HomeFocusBorder;
        private const int FocusPad = UiTheme.HomeFocusPad;
        private const int ContentViewportTopInset = 4;
        private const int ContentViewportStartY = TopBarHeight + ContentViewportTopInset;
        private const int LibraryTitleImageGap = 12;
        private const float FocusScale = UiTheme.MediaCardFocusScale;
        private static readonly bool UseLightweightFocusMode = true;

        private readonly List<HomeRowData> _rows;
        private readonly List<List<View>> _rowCards = new();
        private readonly List<View> _rowContainers = new();
        private readonly List<View> _rowViewports = new();
        private readonly List<int> _rowCardWidths = new();
        private readonly List<int> _rowCardHeights = new();
        private readonly List<int> _rowSpacings = new();
        private readonly List<int> _rowTops = new();
        private readonly List<int> _rowHeights = new();
        private readonly Dictionary<View, Animation> _focusAnimations = new();

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

        private Animation _horizontalScrollAnimation;
        private Animation _verticalScrollAnimation;

        private readonly Color _focusColor = UiTheme.MediaCardFocusBorder;
        private readonly Color _focusBorderColor = UiTheme.MediaCardFocusFill;

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

        public override void OnHide()
        {
            UiAnimator.StopAndDispose(ref _horizontalScrollAnimation);
            UiAnimator.StopAndDispose(ref _verticalScrollAnimation);
            UiAnimator.StopAndDisposeAll(_focusAnimations);
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

                var rowInfo = GetRowStyle(row.Kind);

                var textHeight = GetCardTextHeight(row);
                var cardHeight = rowInfo.CardHeight + textHeight;
                var rowHeight = rowInfo.RowHeight + textHeight;
                //var titleImageGap = row.Kind == HomeRowKind.Libraries ? LibraryTitleImageGap : 0;
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

                _verticalContainer.Add(rowBlock);

                _rowCards.Add(cards);
                _rowContainers.Add(rowContainer);
                _rowViewports.Add(viewport);
                _rowCardWidths.Add(rowInfo.CardWidth);
                _rowCardHeights.Add(cardHeight);
                _rowSpacings.Add(rowInfo.Spacing);
                _rowTops.Add(y);
                _rowHeights.Add(rowHeight + (FocusPad * 2));

                y += rowHeight + (FocusPad * 2) + rowInfo.RowSpacing;
            }
        }

        private int GetCardTextHeight(HomeRowData row)
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
                subtitlePoint: (int)UiTheme.MediaCardSubtitle
            );
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

            if (focused)
            {
                AnimateCardScale(scaleTarget, new Vector3(FocusScale, FocusScale, 1f));
                if (frame != null)
                {
                    frame.PositionZ = 30;
                    card.PositionZ = 0;
                }
                else
                {
                    card.PositionZ = 30;
                }

                MediaCardFocus.ApplyFrameFocus(frame, _focusBorderColor, _focusColor, UseLightweightFocusMode);
            }
            else
            {
                AnimateCardScale(scaleTarget, Vector3.One);
                if (frame != null)
                    frame.PositionZ = 0;
                card.PositionZ = 0;

                MediaCardFocus.ClearFrameFocus(frame);
            }
        }

        private void AnimateCardScale(View card, Vector3 targetScale)
        {
            if (card == null)
            {
                return;
            }

            if (UseLightweightFocusMode)
            {
                if (_focusAnimations.TryGetValue(card, out var existingDirect))
                {
                    UiAnimator.StopAndDispose(ref existingDirect);
                    _focusAnimations.Remove(card);
                }

                card.Scale = targetScale;
                return;
            }

            if (_focusAnimations.TryGetValue(card, out var existing))
            {
                UiAnimator.StopAndDispose(ref existing);
                _focusAnimations.Remove(card);
            }

            var animation = UiAnimator.Start(
                UiAnimator.FocusDurationMs,
                anim => anim.AnimateTo(card, "Scale", targetScale),
                () => _focusAnimations.Remove(card)
            );

            _focusAnimations[card] = animation;
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
        }

        private void ActivateFocusedCard()
        {
            var row = _rows[_rowIndex];
            var item = row.Items[_colIndex];

            if (row.Kind == HomeRowKind.Libraries && item.Library != null)
            {
                OpenLibrary(item.Library);
                return;
            }

            if (item.Media != null)
            {
                if (item.Media.ItemType == "Series")
                {
                    NavigationService.NavigateWithLoading(
                        () => new SeriesDetailsScreen(item.Media),
                        "Loading details..."
                    );
                }
                else if (item.Media.ItemType == "Episode")
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

        private async void OpenLibrary(JellyfinLibrary lib)
        {
            var loadingShownAt = DateTime.UtcNow;
            RunOnUiThread(() =>
            {
                NavigationService.Navigate(
                    new LoadingScreen("Loading items...")
                );
            });

            var includeTypes = lib.CollectionType == "tvshows"
                ? "Series"
                : "Movie";

            var items = await AppState.Jellyfin.GetLibraryItemsAsync(lib.Id, includeTypes);
            var elapsedMs = (DateTime.UtcNow - loadingShownAt).TotalMilliseconds;
            if (elapsedMs < 280)
            {
                await System.Threading.Tasks.Task.Delay((int)(280 - elapsedMs));
            }

            RunOnUiThread(() =>
            {
                NavigationService.Navigate(
                    new LibraryMoviesGridScreen(lib.Name, items),
                    addToStack: false,
                    animated: false
                );
            });
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

            RunOnUiThread(() =>
            {
                NavigationService.Navigate(
                    new LoadingScreen("Fetching users..."),
                    addToStack: false
                );
            });

            var users = await AppState.Jellyfin.GetPublicUsersAsync();
            RunOnUiThread(() =>
            {
                NavigationService.Navigate(
                    new UserSelectScreen(users),
                    addToStack: false
                );
            });
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
    }
}
