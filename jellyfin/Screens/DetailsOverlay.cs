using System.Collections.Generic;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.UI;
using JellyfinTizen.Utils;

namespace JellyfinTizen.Screens
{
    // Second part of the DetailsScreenBase partial class. Holds the shared details "overlay"
    // tree construction relocated out of DetailsScreenBase.cs. This is a pure file split:
    // the methods and const are byte-for-byte the same as before and, being part of the same
    // class, retain unchanged access to all DetailsScreenBase members they reference
    // (_infoColumn, _metadataContainer, _overviewViewport, _overviewLabel, _buttonGroup,
    // _buttonRowTop/_buttonRowBottom, the action-button fields, _resumeAvailable,
    // GetMediaItem(), CreateMetadataView(), UpdateMetadataView(), CreateActionButton(),
    // RebuildActionButtons(), NormalizeSelectionStateForCurrentMediaSource()).
    public abstract partial class DetailsScreenBase
    {
        // =====================================================================
        // OVERLAY TREE CONSTRUCTION (shared skeleton)
        //
        // Collapses the previously-duplicated MovieDetailsScreen/EpisodeDetailsScreen
        // constructor bodies into one parameterized builder. The per-screen differences —
        // backdrop URL, the poster/thumb media frame, the title view(s), and the overview
        // text — are supplied by the caller; everything else (dim, layout, metadata,
        // overview viewport, action buttons, selection normalization) is identical and
        // lives here. Behavior is byte-for-byte equivalent to the prior inline construction:
        // same view tree, same child order, same fade durations, same button set.
        // =====================================================================
        protected const int FixedTopContentHeight = 500;

        protected void BuildDetailsOverlay(
            string backdropUrl,
            View mediaFrame,
            IReadOnlyList<View> titleViews,
            string overviewText)
        {
            var root = UiFactory.CreateAtmosphericBackground();
            bool hasBackdropImage = !string.IsNullOrWhiteSpace(backdropUrl);
            var backdrop = new ImageView
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                PreMultipliedAlpha = false
            };
            UiAnimator.FadeInOnImageReady(backdrop, backdropUrl, UiAnimator.BackdropFadeInDurationMs);
            var dimOverlay = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                BackgroundColor = hasBackdropImage ? UiTheme.DetailsBackdropDim : Color.Transparent
            };
            var content = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                Padding = new Extents(90, 90, 80, 80),
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    CellPadding = new Size2D(60, 0)
                }
            };
            _infoColumn = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    CellPadding = new Size2D(0, 26)
                }
            };
            _metadataContainer = CreateMetadataView();
            var topContentViewport = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = FixedTopContentHeight,
                ClippingMode = ClippingModeType.ClipChildren
            };
            var topContent = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    CellPadding = new Size2D(0, 26)
                }
            };
            _overviewViewport = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = FixedOverviewViewportHeight,
                ClippingMode = ClippingModeType.ClipChildren
            };
            _overviewLabel = new TextLabel(overviewText)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                PointSize = 31,
                TextColor = UiTheme.DetailsOverviewText,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word,
                Ellipsis = false,
                VerticalAlignment = VerticalAlignment.Top
            };
            _overviewViewport.Add(_overviewLabel);

            if (titleViews != null)
            {
                foreach (var titleView in titleViews)
                {
                    if (titleView != null)
                        topContent.Add(titleView);
                }
            }
            topContent.Add(_metadataContainer);
            topContent.Add(_overviewViewport);
            topContentViewport.Add(topContent);
            _infoColumn.Add(topContentViewport);
            UpdateMetadataView();

            if (GetMediaItem().IsPlayableVideo)
            {
                BuildActionButtonGroup();
                _infoColumn.Add(_buttonGroup);
            }

            content.Add(mediaFrame);
            content.Add(_infoColumn);
            root.Add(backdrop);
            root.Add(dimOverlay);
            root.Add(content);
            Add(root);
            NormalizeSelectionStateForCurrentMediaSource();
        }

        // Identical action-button group construction previously duplicated in both
        // screen constructors. Populates _buttonGroup/_buttonRowTop/_buttonRowBottom and the
        // Play/Resume/Audio/Subtitle/Version button fields, then performs the initial reflow.
        protected void BuildActionButtonGroup()
        {
            _buttonGroup = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    CellPadding = new Size2D(0, 14)
                },
                Margin = new Extents(0, 0, 34, 0)
            };

            _buttonRowTop = DetailsScreenHelpers.CreateButtonRow();
            _buttonRowBottom = DetailsScreenHelpers.CreateButtonRow();
            _buttonGroup.Add(_buttonRowTop);
            _buttonGroup.Add(_buttonRowBottom);

            _playButton = CreateActionButton("Play", isPrimary: true, iconFile: "play.svg", width: DetailsScreenHelpers.PlayActionButtonWidth, iconSize: DetailsScreenHelpers.PlayActionButtonIconSize);

            if (_resumeAvailable)
            {
                _resumeButton = CreateActionButton("Resume", isPrimary: false, iconFile: "resume.svg", width: null, iconSize: DetailsScreenHelpers.PlayActionButtonIconSize);
            }

            _audioButton = CreateActionButton(string.Empty, isPrimary: false, iconFile: "audio.svg", width: DetailsScreenHelpers.IconActionButtonWidth, iconSize: DetailsScreenHelpers.AudioActionButtonIconSize);
            _subtitleButton = CreateActionButton(string.Empty, isPrimary: false, iconFile: "sub.svg", width: DetailsScreenHelpers.IconActionButtonWidth, iconSize: DetailsScreenHelpers.SubtitleActionButtonIconSize);
            _versionButton = CreateActionButton("Default", isPrimary: false);
            RebuildActionButtons(includeVersionButton: false);
        }
    }
}
