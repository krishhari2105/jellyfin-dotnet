using System;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Utils;

namespace JellyfinTizen.UI
{
    public static class MediaCardFactory
    {
        // Text height estimation
        private const int MinTextHeightCandidate = 48;
        private const int MaxTextHeightBuffer = 32;
        private const int TextHeightStepIncrement = 8;

        // CreateImageCard defaults
        private const int DefaultFocusBorder = 5;
        private const int DefaultTitlePointSize = 26;
        private const int DefaultSubtitlePointSize = 20;

        // Layout padding/margins
        private const int FramePadding = 2;
        private const int TextContainerPaddingLeftRight = 8;
        private const int TextContainerPaddingTop = 16;
        private const int TextContainerPaddingBottom = 12;
        private const int SubtitleCellPadding = 4;
        private const int NoSubtitleCellPadding = 0;

        // Text measurement
        private const int MinSafeWidth = 120;
        private const int SafeWidthMargin = 16;
        private const float TitleLineHeightMultiplier = 1.28f;
        private const float SubtitleLineHeightMultiplier = 1.30f;
        private const int ContentGapWithSubtitle = 4;
        private const int VerticalPadding = 28;

        // Adaptive title sizing
        private const int SubtitleReserveHeight = 30;
        private const int MinAvailableTitleHeight = 36;
        private const float MinPointSizeReduction = 10f;
        private const float AbsoluteMinPointSize = 18f;
        private const float PointSizeStepDecrement = 2f;

        // Wrapped line estimation
        private const float MinCharWidthMultiplier = 6f;
        private const float CharWidthRatio = 0.54f;
        private const int MinMaxCharsPerLine = 8;
        private const int MinLineCount = 1;

        // ImageContainer background
        private static readonly Color ImageContainerBackground = new Color(0.12f, 0.12f, 0.12f, 1f);

        public static int GetRecommendedTextHeight(
            int width,
            int preferredTextHeight,
            string title,
            string subtitle = null,
            int titlePoint = 26,
            int subtitlePoint = 20)
        {
            bool hasSubtitle = !string.IsNullOrWhiteSpace(subtitle);
            int candidate = Math.Max(MinTextHeightCandidate, preferredTextHeight);
            int maxTextHeight = candidate + Math.Max(MaxTextHeightBuffer, preferredTextHeight / 2);

            while (candidate <= maxTextHeight)
            {
                float adaptiveTitlePoint = ResolveAdaptiveTitlePointSize(
                    title,
                    titlePoint,
                    width,
                    candidate,
                    hasSubtitle);

                if (EstimateRequiredTextHeight(title, subtitle, width, adaptiveTitlePoint, subtitlePoint) <= candidate)
                    return candidate;

                candidate += TextHeightStepIncrement;
            }

            return maxTextHeight;
        }

        public static View CreateImageCard(
            int width,
            int imageHeight,
            int textHeight,
            string title,
            string subtitle,
            string imageUrl,
            out ImageView imageView,
            out View playedBadge,
            int focusBorder = DefaultFocusBorder,
            int titlePoint = DefaultTitlePointSize,
            int subtitlePoint = DefaultSubtitlePointSize,
            float? progressRatio = null,
            bool played = false)
        {
            _ = focusBorder;
            bool hasSubtitle = !string.IsNullOrWhiteSpace(subtitle);
            bool showProgress = progressRatio.HasValue && progressRatio.Value > 0f;
            float clampedProgress = Math.Clamp(progressRatio ?? 0f, 0f, 1f);
            float adaptiveTitlePoint = ResolveAdaptiveTitlePointSize(
                title,
                titlePoint,
                width,
                textHeight,
                hasSubtitle);

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

            var frame = new View
            {
                Name = "CardFrame",
                WidthSpecification = width,
                HeightSpecification = imageHeight,
                PivotPoint = PivotPoint.BottomCenter,
                CornerRadius = UiTheme.MediaCardRadius,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BackgroundColor = Color.Transparent,
                ClippingMode = ClippingModeType.ClipChildren,
                Padding = new Extents(FramePadding, FramePadding, FramePadding, FramePadding),
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal
                }
            };

            var imageContainer = new View
            {
                Name = "CardContent",
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                ClippingMode = ClippingModeType.ClipChildren,
                CornerRadius = UiTheme.MediaCardRadius,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BackgroundColor = ImageContainerBackground
            };

            imageView = new ImageView
            {
                Name = "CardImage",
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                PreMultipliedAlpha = false
            };
            UiAnimator.FadeInOnImageReady(imageView, imageUrl);
            imageContainer.Add(imageView);

            playedBadge = new View
            {
                Name = "CardPlayedBadgeBackdrop",
                WidthSpecification = 48,
                HeightSpecification = 48,
                BackgroundColor = new Color(0f, 0f, 0f, 0.65f),
                CornerRadius = 24.0f,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                ParentOrigin = Tizen.NUI.ParentOrigin.TopRight,
                PivotPoint = Tizen.NUI.PivotPoint.TopRight,
                PositionUsesPivotPoint = true,
                PositionX = -10,
                PositionY = 10,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            var badgeIcon = new ImageView
            {
                Name = "CardPlayedBadgeIcon",
                WidthSpecification = 30,
                HeightSpecification = 30,
                ResourceUrl = System.IO.Path.Combine(Tizen.Applications.Application.Current.DirectoryInfo.SharedResource, "check_circle_white.svg"),
                PreMultipliedAlpha = false,
                FittingMode = FittingModeType.ShrinkToFit,
                SamplingMode = SamplingModeType.BoxThenLanczos
            };

            playedBadge.Add(badgeIcon);
            imageContainer.Add(playedBadge);

            if (played)
            {
                playedBadge.Show();
            }
            else
            {
                playedBadge.Hide();
            }

            if (showProgress)
            {
                int progressWidth = Math.Max(0, width - (UiTheme.MediaCardProgressInset * 2));
                int progressFillWidth = Math.Max(2, (int)Math.Round(progressWidth * clampedProgress));
                int progressY = imageHeight - UiTheme.MediaCardProgressBottomInset - UiTheme.MediaCardProgressHeight;

                var progressTrack = new View
                {
                    Name = "CardProgressTrack",
                    WidthSpecification = progressWidth,
                    HeightSpecification = UiTheme.MediaCardProgressHeight,
                    PositionX = UiTheme.MediaCardProgressInset,
                    PositionY = progressY,
                    BackgroundColor = UiTheme.MediaCardProgressTrack,
                    CornerRadius = UiTheme.MediaCardProgressRadius,
                    CornerRadiusPolicy = VisualTransformPolicyType.Absolute
                };

                var progressFill = new View
                {
                    Name = "CardProgressFill",
                    WidthSpecification = progressFillWidth,
                    HeightSpecification = UiTheme.MediaCardProgressHeight,
                    BackgroundColor = UiTheme.MediaCardProgressFill,
                    CornerRadius = UiTheme.MediaCardProgressRadius,
                    CornerRadiusPolicy = VisualTransformPolicyType.Absolute
                };

                progressTrack.Add(progressFill);
                imageContainer.Add(progressTrack);
            }

            frame.Add(imageContainer);

            var textContainer = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = textHeight,
                BackgroundColor = Color.Transparent,
                Padding = new Extents(TextContainerPaddingLeftRight, TextContainerPaddingLeftRight, TextContainerPaddingTop, TextContainerPaddingBottom),
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    CellPadding = new Size2D(0, hasSubtitle ? SubtitleCellPadding : NoSubtitleCellPadding)
                }
            };

            textContainer.Add(new TextLabel(title ?? string.Empty)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                PointSize = adaptiveTitlePoint,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word,
                Ellipsis = true
            });

            if (hasSubtitle)
            {
                textContainer.Add(new TextLabel(subtitle)
                {
                    WidthResizePolicy = ResizePolicyType.FillToParent,
                    HeightResizePolicy = ResizePolicyType.FitToChildren,
                    TextColor = new Color(1, 1, 1, 0.75f),
                    PointSize = subtitlePoint,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    MultiLine = false,
                    Ellipsis = true
                });
            }

            wrapper.Add(frame);
            wrapper.Add(textContainer);
            return wrapper;
        }

        private static int EstimateRequiredTextHeight(
            string title,
            string subtitle,
            int availableWidth,
            float titlePointSize,
            float subtitlePointSize)
        {
            bool hasSubtitle = !string.IsNullOrWhiteSpace(subtitle);
            int safeWidth = Math.Max(MinSafeWidth, availableWidth - SafeWidthMargin);
            int lineCount = EstimateWrappedLineCount(title, safeWidth, titlePointSize);
            int titleHeight = (int)Math.Ceiling(lineCount * (titlePointSize * TitleLineHeightMultiplier));
            int subtitleHeight = hasSubtitle
                ? (int)Math.Ceiling(subtitlePointSize * SubtitleLineHeightMultiplier)
                : 0;
            int contentGap = hasSubtitle ? ContentGapWithSubtitle : 0;
            int verticalPadding = VerticalPadding;

            return verticalPadding + titleHeight + contentGap + subtitleHeight;
        }

        private static float ResolveAdaptiveTitlePointSize(
            string title,
            float preferredPointSize,
            int availableWidth,
            int textHeight,
            bool hasSubtitle)
        {
            if (string.IsNullOrWhiteSpace(title))
                return preferredPointSize;

            int safeWidth = Math.Max(MinSafeWidth, availableWidth - SafeWidthMargin);
            int subtitleReserve = hasSubtitle ? SubtitleReserveHeight : 0;
            int verticalPadding = VerticalPadding;
            int availableTitleHeight = Math.Max(MinAvailableTitleHeight, textHeight - verticalPadding - subtitleReserve);
            float pointSize = preferredPointSize;
            float minimumPointSize = Math.Max(AbsoluteMinPointSize, preferredPointSize - MinPointSizeReduction);

            while (pointSize > minimumPointSize)
            {
                int estimatedLineCount = EstimateWrappedLineCount(title, safeWidth, pointSize);
                float estimatedHeight = estimatedLineCount * (pointSize * TitleLineHeightMultiplier);
                if (estimatedHeight <= availableTitleHeight)
                    break;

                pointSize -= PointSizeStepDecrement;
            }

            return Math.Max(minimumPointSize, pointSize);
        }

        private static int EstimateWrappedLineCount(string text, int availableWidth, float pointSize)
        {
            if (string.IsNullOrWhiteSpace(text))
                return MinLineCount;

            float approximateCharWidth = Math.Max(MinCharWidthMultiplier, pointSize * CharWidthRatio);
            int maxCharsPerLine = Math.Max(MinMaxCharsPerLine, (int)Math.Floor(availableWidth / approximateCharWidth));
            int lineCount = 0;

            foreach (var paragraph in text.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(paragraph))
                {
                    lineCount += MinLineCount;
                    continue;
                }

                int currentLineLength = 0;
                foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    int wordLength = word.Length;
                    if (currentLineLength == 0)
                    {
                        lineCount += Math.Max(MinLineCount, (int)Math.Ceiling(wordLength / (double)maxCharsPerLine));
                        currentLineLength = wordLength % maxCharsPerLine;
                        if (currentLineLength == 0 && wordLength > 0)
                            currentLineLength = maxCharsPerLine;
                        continue;
                    }

                    int requiredLength = currentLineLength + 1 + wordLength;
                    if (requiredLength <= maxCharsPerLine)
                    {
                        currentLineLength = requiredLength;
                        continue;
                    }

                    int overflowLength = wordLength;
                    lineCount += MinLineCount;
                    currentLineLength = 0;

                    if (overflowLength >= maxCharsPerLine)
                    {
                        int extraLines = (int)Math.Ceiling(overflowLength / (double)maxCharsPerLine);
                        lineCount += extraLines - 1;
                        currentLineLength = overflowLength % maxCharsPerLine;
                        if (currentLineLength == 0)
                            currentLineLength = maxCharsPerLine;
                    }
                    else
                    {
                        currentLineLength = overflowLength;
                    }
                }

                if (currentLineLength == 0)
                    lineCount += MinLineCount;
            }

            return Math.Max(MinLineCount, lineCount);
        }
    }
}
