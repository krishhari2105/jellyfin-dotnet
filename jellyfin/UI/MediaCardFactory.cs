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
                CornerRadius = 16.0f,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BackgroundColor = Color.Transparent,
                Padding = new Extents(
                    (ushort)focusBorder,
                    (ushort)focusBorder,
                    (ushort)focusBorder,
                    (ushort)focusBorder),
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
                BackgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f)
            };

            var imageContainer = new View
            {
                Name = "CardContent",
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                ClippingMode = ClippingModeType.ClipChildren
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
            inner.Add(imageContainer);
            frame.Add(inner);

            var textContainer = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = textHeight,
                BackgroundColor = Color.Transparent,
                Padding = new Extents(8, 8, 12, 0),
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
                LineWrapMode = LineWrapMode.Word
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
                    MultiLine = false
                });
            }

            wrapper.Add(frame);
            wrapper.Add(textContainer);
            return wrapper;
        }
    }
}
