using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace JellyfinTizen.UI
{
    public static class MediaCardFactory
    {
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
            int subtitlePoint = 20)
        {
            _ = focusBorder;

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
            frame.Add(imageContainer);

            var textContainer = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = textHeight,
                BackgroundColor = Color.Transparent,
                Padding = new Extents(8, 8, 20, 8),
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    CellPadding = new Size2D(0, 2)
                }
            };

            textContainer.Add(new TextLabel(title ?? string.Empty)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                PointSize = titlePoint,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word,
                Ellipsis = true
            });

            if (!string.IsNullOrWhiteSpace(subtitle))
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
    }
}
