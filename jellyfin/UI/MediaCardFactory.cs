using System;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace JellyfinTizen.UI
{
    public static class MediaCardFactory
    {
        public static int GetRecommendedTextHeight(
            int width,
            int preferredTextHeight,
            string title,
            string subtitle = null,
            int titlePoint = 26,
            int subtitlePoint = 20)
        {
            bool hasSubtitle = !string.IsNullOrWhiteSpace(subtitle);
            int candidate = Math.Max(48, preferredTextHeight);
            int maxTextHeight = candidate + Math.Max(32, preferredTextHeight / 2);

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

                candidate += 8;
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
            int focusBorder = 5,
            int titlePoint = 26,
            int subtitlePoint = 20,
            float? progressRatio = null)
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
                Padding = new Extents(2, 2, 2, 2),
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
                BackgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f)
            };

            imageView = new ImageView
            {
                Name = "CardImage",
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                ResourceUrl = imageUrl,
                PreMultipliedAlpha = false
            };
            imageContainer.Add(imageView);

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
                Padding = new Extents(8, 8, 16, 12),
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    CellPadding = new Size2D(0, hasSubtitle ? 4 : 0)
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
            int safeWidth = Math.Max(120, availableWidth - 16);
            int lineCount = EstimateWrappedLineCount(title, safeWidth, titlePointSize);
            int titleHeight = (int)Math.Ceiling(lineCount * (titlePointSize * 1.28f));
            int subtitleHeight = hasSubtitle
                ? (int)Math.Ceiling(subtitlePointSize * 1.30f)
                : 0;
            int contentGap = hasSubtitle ? 4 : 0;
            int verticalPadding = 28;

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

            int safeWidth = Math.Max(120, availableWidth - 16);
            int subtitleReserve = hasSubtitle ? 30 : 0;
            int verticalPadding = 28;
            int availableTitleHeight = Math.Max(36, textHeight - verticalPadding - subtitleReserve);
            float pointSize = preferredPointSize;
            float minimumPointSize = Math.Max(18f, preferredPointSize - 10f);

            while (pointSize > minimumPointSize)
            {
                int estimatedLineCount = EstimateWrappedLineCount(title, safeWidth, pointSize);
                float estimatedHeight = estimatedLineCount * (pointSize * 1.28f);
                if (estimatedHeight <= availableTitleHeight)
                    break;

                pointSize -= 2f;
            }

            return Math.Max(minimumPointSize, pointSize);
        }

        private static int EstimateWrappedLineCount(string text, int availableWidth, float pointSize)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 1;

            float approximateCharWidth = Math.Max(6f, pointSize * 0.54f);
            int maxCharsPerLine = Math.Max(8, (int)Math.Floor(availableWidth / approximateCharWidth));
            int lineCount = 0;

            foreach (var paragraph in text.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(paragraph))
                {
                    lineCount++;
                    continue;
                }

                int currentLineLength = 0;
                foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    int wordLength = word.Length;
                    if (currentLineLength == 0)
                    {
                        lineCount += Math.Max(1, (int)Math.Ceiling(wordLength / (double)maxCharsPerLine));
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
                    lineCount++;
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
                    lineCount++;
            }

            return Math.Max(1, lineCount);
        }
    }
}
