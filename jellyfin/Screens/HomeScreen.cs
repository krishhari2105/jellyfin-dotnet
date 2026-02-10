using System;
using System.Collections.Generic;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Core;
using JellyfinTizen.Models;

namespace JellyfinTizen.Screens
{
    public class HomeScreen : ScreenBase, IKeyHandler
    {
        private const int TopBarHeight = 72;
        private const int RowsStartY = TopBarHeight + 28;
        private const int SidePadding = 60;
        private const int FocusBorder = 4;
        private const int FocusPad = 20;
        private const float FocusScale = 1.14f;

        private readonly List<HomeRowData> _rows;
        private readonly List<List<View>> _rowCards = new();
        private readonly List<View> _rowContainers = new();
        private readonly List<View> _rowViewports = new();
        private readonly List<int> _rowCardWidths = new();
        private readonly List<int> _rowCardHeights = new();
        private readonly List<int> _rowSpacings = new();
        private readonly List<int> _rowTops = new();
        private readonly List<int> _rowHeights = new();

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

        // Jellyfin Blue (#00A4DC)
        private readonly Color _focusColor = new Color(0.0f, 0.64f, 0.86f, 1.0f);
        private readonly Color _focusBorderColor = new Color(0.0f, 0.64f, 0.86f, 0.45f);

        public HomeScreen(List<HomeRowData> rows)
        {
            _rows = rows ?? new List<HomeRowData>();

            var root = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent
            };

            var topBar = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = TopBarHeight,
                PositionZ = 10,
                BackgroundColor = Color.Black,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    CellPadding = new Size2D(16, 0)
                },
                Padding = new Extents(SidePadding, SidePadding, 16, 0)
            };

            var title = new TextLabel("Jellyfin")
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                PointSize = 46,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Begin
            };

            _settingsButton = new View
            {
                WidthSpecification = 180,
                HeightSpecification = 52,
                BackgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f),
                Focusable = true,
                CornerRadius = 26f
            };

            var settingsLabel = new TextLabel("Settings")
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                PointSize = 24,
                TextColor = Color.White
            };

            _settingsButton.Add(settingsLabel);

            topBar.Add(title);
            topBar.Add(_settingsButton);

            _verticalContainer = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                PositionY = RowsStartY,
                PositionZ = 0
            };

            root.Add(_verticalContainer);
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
            _settingsPanel?.Hide();

            Highlight(true);
            ScrollHorizontalIfNeeded();
            ScrollVerticalIfNeeded();
            try
            {
                Tizen.Applications.CoreApplication.Post(() =>
                {
                    FocusManager.Instance.SetCurrentFocusView(_rowCards[_rowIndex][_colIndex]);
                });
            }
            catch
            {
                FocusManager.Instance.SetCurrentFocusView(_rowCards[_rowIndex][_colIndex]);
            }
        }

        private void BuildRows()
        {
            int y = 0;

            foreach (var row in _rows)
            {
                if (row.Items == null || row.Items.Count == 0)
                    continue;

                var rowInfo = GetRowStyle(row.Kind);

                var textHeight = GetCardTextHeight(row.Kind);
                var cardHeight = rowInfo.CardHeight + textHeight;
                var rowHeight = rowInfo.RowHeight + textHeight;

                var rowBlock = new View
                {
                    WidthResizePolicy = ResizePolicyType.FillToParent,
                    HeightSpecification = rowHeight + (FocusPad * 2),
                    PositionY = y
                };

                var title = new TextLabel(row.Title)
                {
                    WidthResizePolicy = ResizePolicyType.FillToParent,
                    HeightSpecification = rowInfo.TitleHeight,
                    PointSize = 34,
                    TextColor = Color.White,
                    HorizontalAlignment = HorizontalAlignment.Begin,
                    Padding = new Extents(SidePadding, SidePadding, 0, 0)
                };

                var viewport = new View
                {
                    WidthResizePolicy = ResizePolicyType.FillToParent,
                    HeightSpecification = cardHeight + (FocusPad * 2),
                    PositionY = rowInfo.TitleHeight + 10,
                    ClippingMode = ClippingModeType.ClipChildren,
                    Padding = new Extents(SidePadding, SidePadding, 0, 0)
                };

                var rowContainer = new View
                {
                    PositionY = FocusPad,
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

        private int GetCardTextHeight(HomeRowKind kind)
        {
            return kind switch
            {
                HomeRowKind.Libraries => 48,
                HomeRowKind.NextUp => 64,
                HomeRowKind.ContinueWatching => 64,
                _ => 60
            };
        }

        private (int CardWidth, int CardHeight, int Spacing, int TitleHeight, int RowHeight, int RowSpacing) GetRowStyle(HomeRowKind kind)
        {
            return kind switch
            {
                HomeRowKind.Libraries => (420, 236, 30, 60, 316, 24),
                HomeRowKind.NextUp => (420, 236, 30, 60, 316, 24),
                HomeRowKind.ContinueWatching => (420, 236, 30, 60, 316, 24),
                _ => (260, 390, 24, 60, 460, 26)
            };
        }

        private View CreateCard(HomeRowKind kind, HomeItemData item, int width, int imageHeight, int textHeight)
        {
            var isLandscapeRow = kind == HomeRowKind.Libraries ||
                                 kind == HomeRowKind.NextUp ||
                                 kind == HomeRowKind.ContinueWatching;

            var hasSubtitle = !string.IsNullOrWhiteSpace(item.Subtitle) && kind != HomeRowKind.Libraries;

            // Wrapper holds image card + separate text below
            var wrapper = new View
            {
                WidthSpecification = width,
                HeightSpecification = imageHeight + textHeight,
                Focusable = true,
                BackgroundColor = Color.Transparent,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical
                }
            };

            // Image card (fixed size)
            var frame = new View
            {
                Name = "CardFrame",
                WidthSpecification = width,
                HeightSpecification = imageHeight,
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
                CornerRadius = 12.0f, // Matches outer radius minus inset roughly
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                ClippingMode = ClippingModeType.ClipChildren,
                BackgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f)
            };

            var imageContainer = new View
            {
                Name = "CardContent",
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                ClippingMode = ClippingModeType.ClipChildren
            };

            if (!string.IsNullOrEmpty(item.ImageUrl))
            {
                var image = new ImageView
                {
                    Name = "CardImage",
                    WidthResizePolicy = ResizePolicyType.FillToParent,
                    HeightResizePolicy = ResizePolicyType.FillToParent,
                    ResourceUrl = item.ImageUrl,
                    PreMultipliedAlpha = false
                };
                imageContainer.Add(image);
            }

            inner.Add(imageContainer);
            frame.Add(inner);

            var textContainer = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = textHeight,
                BackgroundColor = Color.Transparent,
                Padding = new Extents(8, 8, 6, 0),
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    CellPadding = new Size2D(0, 2)
                }
            };

            var title = new TextLabel(item.Title ?? string.Empty)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                PointSize = isLandscapeRow ? 22 : 24,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word
            };
            textContainer.Add(title);

            if (hasSubtitle)
            {
                var subtitle = new TextLabel(item.Subtitle)
                {
                    WidthResizePolicy = ResizePolicyType.FillToParent,
                    HeightResizePolicy = ResizePolicyType.FitToChildren,
                    TextColor = new Color(1, 1, 1, 0.75f),
                    PointSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    MultiLine = false
                };
                textContainer.Add(subtitle);
            }

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
            Highlight(false);

            _rowIndex = Math.Clamp(_rowIndex + rowDelta, 0, _rowCards.Count - 1);
            _colIndex = Math.Clamp(_colIndex + colDelta, 0, _rowCards[_rowIndex].Count - 1);

            Highlight(true);
            FocusManager.Instance.SetCurrentFocusView(_rowCards[_rowIndex][_colIndex]);

            ScrollHorizontalIfNeeded();
            ScrollVerticalIfNeeded();
        }

        private void Highlight(bool focused)
        {
            var card = _rowCards[_rowIndex][_colIndex];
            var frame = GetCardFrame(card);
            var content = GetCardContent(card);

            // Ensure the card itself stays rounded
            if (frame != null)
                frame.CornerRadius = 16.0f;

            if (focused)
            {
                if (content != null)
                    content.Scale = new Vector3(FocusScale, FocusScale, 1f);

                // Keep outer size stable to avoid viewport clipping
                card.Scale = Vector3.One;
                card.PositionZ = 30;

                // KEY FIX: Use BackgroundColor as the border mechanism.
                // Since 'card' has Padding(5), this fills the gap with color, creating a frame.
                // BackgroundColor respects CornerRadius much better than BorderlineWidth in some cases.
                if (frame != null)
                {
                    frame.BackgroundColor = _focusBorderColor;
                    frame.BorderlineWidth = 0.0f; // Disable explicit border line
                    frame.BoxShadow = new Shadow(8.0f, new Color(0.0f, 0.64f, 0.86f, 0.25f), new Vector2(0, 0));
                }
            }
            else
            {
                if (content != null)
                    content.Scale = Vector3.One;

                card.Scale = Vector3.One;
                card.PositionZ = 0;

                // Remove focus effects
                if (frame != null)
                {
                    frame.BackgroundColor = Color.Transparent;
                    frame.BorderlineWidth = 0.0f;
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

        private View GetCardContent(View card)
        {
            foreach (var child in card.Children)
            {
                if (child.Name == "CardFrame")
                {
                    foreach (var frameChild in child.Children)
                    {
                        if (frameChild.Name == "CardInner")
                        {
                            foreach (var innerChild in frameChild.Children)
                            {
                                if (innerChild.Name == "CardContent")
                                    return innerChild;
                            }
                        }
                    }
                }
            }
            return null;
        }

        private void ScrollHorizontalIfNeeded()
        {
            var rowContainer = _rowContainers[_rowIndex];
            var viewport = _rowViewports[_rowIndex];
            var focused = _rowCards[_rowIndex][_colIndex];

            if (_colIndex == 0)
            {
                rowContainer.PositionX = SidePadding;
                return;
            }

            var offset = -rowContainer.PositionX;
            var viewportWidth = viewport.SizeWidth;

            var left = focused.PositionX;
            var right = left + _rowCardWidths[_rowIndex];

            var visibleLeft = offset;
            var visibleRight = offset + viewportWidth;

            var spacing = _rowSpacings[_rowIndex];

            if (right > visibleRight)
                rowContainer.PositionX -= (right - visibleRight + spacing);
            else if (left < visibleLeft)
                rowContainer.PositionX += (visibleLeft - left + spacing);
        }

        private void ScrollVerticalIfNeeded()
        {
            var viewportHeight = Window.Default.Size.Height;
            var currentOffset = -_verticalContainer.PositionY + RowsStartY;

            var rowTop = _rowTops[_rowIndex];
            var rowBottom = rowTop + _rowHeights[_rowIndex];

            var visibleTop = currentOffset;
            var visibleBottom = currentOffset + viewportHeight - 200;

            if (rowBottom > visibleBottom)
            {
                var delta = rowBottom - visibleBottom;
                _verticalContainer.PositionY -= delta;
            }
            else if (rowTop < visibleTop)
            {
                var delta = visibleTop - rowTop;
                _verticalContainer.PositionY += delta;
            }

            if (_verticalContainer.PositionY > RowsStartY)
                _verticalContainer.PositionY = RowsStartY;
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
                    NavigationService.Navigate(
                        new SeriesDetailsScreen(item.Media)
                    );
                }
                else if (item.Media.ItemType == "Episode")
                {
                    NavigationService.Navigate(
                        new EpisodeDetailsScreen(item.Media)
                    );
                }
                else // Movie
                {
                    NavigationService.Navigate(
                        new MovieDetailsScreen(item.Media)
                    );
                }
            }
        }

        private async void OpenLibrary(JellyfinLibrary lib)
        {
            NavigationService.Navigate(
                new LoadingScreen("Loading items...")
            );

            var includeTypes = lib.CollectionType == "tvshows"
                ? "Series"
                : "Movie";

            var items = await AppState.Jellyfin.GetLibraryItemsAsync(lib.Id, includeTypes);

            NavigationService.Navigate(
                new LibraryMoviesGridScreen(lib.Name, items),
                addToStack: false
            );
        }

        private void FocusSettings(bool focused)
        {
            _settingsFocused = focused;

            if (focused)
            {
                _settingsButton.Scale = new Vector3(1.1f, 1.1f, 1f);
                _settingsButton.BackgroundColor = new Color(0.35f, 0.35f, 0.35f, 1f);
                _settingsButton.BorderlineWidth = 0.0f;
                _settingsButton.BoxShadow = null;

                Highlight(false);
                FocusManager.Instance.SetCurrentFocusView(_settingsButton);
            }
            else
            {
                _settingsButton.Scale = Vector3.One;
                _settingsButton.BackgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
                _settingsButton.BorderlineWidth = 0.0f;
                _settingsButton.BoxShadow = null;

                Highlight(true);
                FocusManager.Instance.SetCurrentFocusView(_rowCards[_rowIndex][_colIndex]);
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
                CornerRadius = 10.0f,
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
    }
}
