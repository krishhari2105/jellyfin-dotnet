using System;
using System.Collections.Generic;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using Tizen.NUI.Components;
using JellyfinTizen.Core;

namespace JellyfinTizen.UI
{
    public sealed class DetailsSelectionOption
    {
        public DetailsSelectionOption(string id, string label)
        {
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
        }

        public string Id { get; }
        public string Label { get; }
    }

    public sealed class DetailsSelectionPanel
    {
        // Match the in-player subtitle panel's 20px bottom breathing room.
        private const int DetailsPanelListHeight =
            UiTheme.PlayerOverlayHeight -
            UiTheme.PlayerOverlayHeaderHeight -
            UiTheme.PlayerOverlayBottomInset;
        private const int DetailsPanelHeight = UiTheme.PlayerOverlayHeight;

        private readonly View _overlay;
        private readonly View _panel;
        private readonly TextLabel _titleLabel;
        private readonly ScrollableBase _scrollView;
        private readonly View _listContainer;
        private readonly List<DetailsSelectionOption> _options = new();
        private int _selectedIndex;

        public DetailsSelectionPanel(View host)
        {
            _overlay = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                BackgroundColor = Color.Transparent
            };
            _overlay.Hide();

            _panel = new View
            {
                WidthSpecification = UiTheme.PlayerOverlayWidth,
                HeightSpecification = DetailsPanelHeight,
                BackgroundColor = MonochromeAuthFactory.PanelFallbackColor,
                PositionX = Math.Max(30, Window.Default.Size.Width - UiTheme.PlayerOverlayWidth - 50),
                PositionY = Math.Max(100, Window.Default.Size.Height - DetailsPanelHeight - 280),
                CornerRadius = MonochromeAuthFactory.PanelCornerRadius,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BorderlineWidth = MonochromeAuthFactory.PanelBorderWidth,
                BorderlineColor = MonochromeAuthFactory.PanelFallbackBorder,
                ClippingMode = ClippingModeType.ClipChildren
            };

            _titleLabel = new TextLabel(string.Empty)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = UiTheme.PlayerOverlayHeaderHeight,
                PointSize = UiTheme.PlayerOverlayHeader,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _panel.Add(_titleLabel);

            _scrollView = new ScrollableBase
            {
                WidthSpecification =
                    UiTheme.PlayerOverlayWidth -
                    (UiTheme.PlayerOverlayListHorizontalInset * 2),
                HeightSpecification = DetailsPanelListHeight,
                PositionX = UiTheme.PlayerOverlayListHorizontalInset,
                PositionY = UiTheme.PlayerOverlayHeaderHeight,
                ScrollingDirection = ScrollableBase.Direction.Vertical,
                BackgroundColor = Color.Transparent
            };

            _listContainer = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    CellPadding =
                        new Size2D(0, UiTheme.PlayerOverlayListCellPadding)
                }
            };

            _scrollView.Add(_listContainer);
            _panel.Add(_scrollView);
            _overlay.Add(_panel);
            host.Add(_overlay);
        }

        public bool IsVisible { get; private set; }

        public void Show(string title, IReadOnlyList<DetailsSelectionOption> options, int selectedIndex, float itemPointSize)
        {
            _titleLabel.Text = title ?? string.Empty;
            _options.Clear();
            if (options != null)
            {
                for (int i = 0; i < options.Count; i++)
                    _options.Add(options[i]);
            }

            RebuildRows(itemPointSize);
            _selectedIndex = _options.Count == 0
                ? 0
                : Math.Clamp(selectedIndex, 0, _options.Count - 1);
            UpdateVisuals();
            ScrollSelectionIntoView();
            _overlay.RaiseToTop();
            _overlay.Show();
            IsVisible = true;
        }

        public void Hide()
        {
            IsVisible = false;
            _overlay.Hide();
        }

        public void MoveSelection(int delta)
        {
            if (!IsVisible || _options.Count == 0)
                return;

            _selectedIndex = Math.Clamp(_selectedIndex + delta, 0, _options.Count - 1);
            UpdateVisuals();
            ScrollSelectionIntoView();
        }

        public bool TryGetSelectedOption(out DetailsSelectionOption option)
        {
            if (_options.Count == 0 || _selectedIndex < 0 || _selectedIndex >= _options.Count)
            {
                option = null;
                return false;
            }

            option = _options[_selectedIndex];
            return true;
        }

        private void RebuildRows(float itemPointSize)
        {
            while (_listContainer.ChildCount > 0)
            {
                var child = _listContainer.GetChildAt(0);
                if (child == null)
                    break;

                _listContainer.Remove(child);
            }

            for (int i = 0; i < _options.Count; i++)
            {
                var row = UiFactory.CreateTrackSelectionRow(
                    _options[i].Label,
                    _options[i].Id,
                    itemPointSize);
                _listContainer.Add(row);
            }

            _listContainer.PositionY = 0;
        }

        private void UpdateVisuals()
        {
            int count = (int)_listContainer.ChildCount;
            for (int i = 0; i < count; i++)
            {
                var row = _listContainer.GetChildAt((uint)i);
                bool selected = i == _selectedIndex;
                UiFactory.SetButtonFocusState(row, focused: selected);
                row.Scale = Vector3.One;
            }
        }

        private void ScrollSelectionIntoView()
        {
            int count = (int)_listContainer.ChildCount;
            if (count <= 0 || _selectedIndex < 0 || _selectedIndex >= count)
                return;

            var selected = _listContainer.GetChildAt((uint)_selectedIndex);
            int viewportHeight = _scrollView.SizeHeight > 0
                ? (int)Math.Round(_scrollView.SizeHeight)
                : DetailsPanelListHeight;
            int nextTop = TrackListScrollCalculator.CalculateTargetOffset(
                _selectedIndex,
                count,
                (int)Math.Round(selected.PositionY),
                (int)Math.Round(selected.SizeHeight),
                (int)Math.Round(_listContainer.SizeHeight),
                viewportHeight,
                (int)(-_listContainer.PositionY),
                UiTheme.PlayerOverlayListRowHeight,
                UiTheme.PlayerOverlayListCellPadding,
                UiTheme.PlayerOverlayItemMarginVertical,
                UiTheme.PlayerOverlayScrollEdgeInset);

            _listContainer.PositionY = -nextTop;
        }
    }
}
