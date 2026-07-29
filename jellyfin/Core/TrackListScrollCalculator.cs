using System;

namespace JellyfinTizen.Core
{
    public static class TrackListScrollCalculator
    {
        public static int CalculateTargetOffset(
            int selectedIndex,
            int itemCount,
            int laidOutRowTop,
            int laidOutRowHeight,
            int laidOutContentHeight,
            int viewportHeight,
            int currentOffset,
            int fallbackRowHeight,
            int cellPadding,
            int verticalMargin,
            int edgeInset)
        {
            if (itemCount <= 0 ||
                selectedIndex < 0 ||
                selectedIndex >= itemCount ||
                viewportHeight <= 0)
            {
                return Math.Max(0, currentOffset);
            }

            int rowHeight = laidOutRowHeight > 0
                ? laidOutRowHeight
                : Math.Max(1, fallbackRowHeight);
            int safeCellPadding = Math.Max(0, cellPadding);
            int safeVerticalMargin = Math.Max(0, verticalMargin);
            int rowStride = rowHeight + (safeVerticalMargin * 2) + safeCellPadding;

            // PositionY is still zero for every freshly-added NUI row until the first
            // layout pass. Derive the selected row's geometry so an initially selected
            // track near the bottom can be revealed before the user presses a key.
            bool rowLayoutReady = selectedIndex == 0 || laidOutRowTop > 0;
            int rowTop = laidOutRowTop;
            if (!rowLayoutReady)
                rowTop = safeVerticalMargin + (selectedIndex * rowStride);
            rowTop = Math.Max(0, rowTop);
            int rowBottom = rowTop + rowHeight;

            int estimatedContentHeight =
                (itemCount * (rowHeight + (safeVerticalMargin * 2))) +
                (Math.Max(0, itemCount - 1) * safeCellPadding);
            int contentHeight = rowLayoutReady && laidOutContentHeight > 0
                ? laidOutContentHeight
                : estimatedContentHeight;
            int maxOffset = Math.Max(0, contentHeight - viewportHeight);
            int currentTop = Math.Clamp(currentOffset, 0, maxOffset);
            int inset = Math.Max(0, edgeInset);
            int currentBottom = currentTop + viewportHeight;

            int nextTop = currentTop;
            if (rowBottom + inset > currentBottom)
                nextTop = rowBottom - viewportHeight + inset;
            else if (rowTop - inset < currentTop)
                nextTop = rowTop - inset;

            return Math.Clamp(nextTop, 0, maxOffset);
        }
    }
}
