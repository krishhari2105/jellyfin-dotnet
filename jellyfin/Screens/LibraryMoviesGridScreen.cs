using System;
using System.Collections.Generic;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Core;
using JellyfinTizen.Models;
using JellyfinTizen.Utils;

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
        private const float FocusScale = 1.08f;
        private static readonly bool UseLightweightFocusMode = true;
        private const int CardTextHeight = 80;
        private const int RowBuildBatchSize = 2;
        private const int PosterVisibleRowBuffer = 1;
        private const int PosterKeepLowRowBuffer = 3;
        private const int PosterRefreshIntervalMs = 260;
        private const int BuildTickMs = 20;
        private const int HighQualityDelayMs = 320;

        // Jellyfin Blue (#00A4DC)
        private readonly Color _focusColor = new Color(0.0f, 0.64f, 0.86f, 1.0f);
        private readonly Color _focusBorderColor = new Color(0.0f, 0.64f, 0.86f, 0.58f);

        private readonly List<JellyfinMovie> _movies;

        private readonly List<List<View>> _grid = new();
        private readonly List<View> _rowContainers = new();
        private readonly List<View> _viewports = new();
        private readonly Dictionary<View, Animation> _focusAnimations = new();
        private readonly Dictionary<ImageView, PosterLoadState> _posterStates = new();

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
                    : sharedResPath + "settings.svg",
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

            BuildPosterUrls(movie, out var posterLowUrl, out var posterHighUrl);

            var poster = new ImageView
            {
                Name = "CardImage",
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                ResourceUrl = null,
                PreMultipliedAlpha = false   // 🔑 CRITICAL on Tizen
            };
            _posterStates[poster] = new PosterLoadState
            {
                LowUrl = posterLowUrl,
                HighUrl = posterHighUrl,
                Row = rowNumber,
                Quality = PosterQuality.Unloaded
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
            int visibleTop = (int)(-_verticalContainer.PositionY + ContentStartY);
            int visibleBottom = visibleTop + Window.Default.Size.Height;

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
            var frame = GetCardFrame(card);
            var content = GetCardContent(card);
            if (content != null)
                AnimateCardScale(content, focused ? new Vector3(FocusScale, FocusScale, 1f) : Vector3.One);

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
                    frame.BoxShadow = UseLightweightFocusMode
                        ? null
                        : new Shadow(12.0f, new Color(0.0f, 0.64f, 0.86f, 0.36f), new Vector2(0, 0));
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

            _settingsPanel = new View
            {
                WidthSpecification = 420,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                BackgroundColor = new Color(0, 0, 0, 1.0f),
                PositionX = _settingsPanelBaseX,
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

            _settingsPanel.Opacity = 1.0f;
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
            var viewportHeight = Window.Default.Size.Height;
            var rowHeight = (PosterHeight + CardTextHeight) + (FocusPad * 2) + RowSpacing;

            var currentOffset = -_verticalContainer.PositionY + ContentStartY;
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

            if (targetY > ContentStartY)
                targetY = ContentStartY;

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
