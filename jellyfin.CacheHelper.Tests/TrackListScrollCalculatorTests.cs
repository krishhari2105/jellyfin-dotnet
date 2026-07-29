using JellyfinTizen.Core;
using Xunit;

namespace JellyfinTizen.CacheHelper.Tests;

public class TrackListScrollCalculatorTests
{
    [Fact]
    public void InitialSelectionUsesFallbackGeometryBeforeLayout()
    {
        int offset = TrackListScrollCalculator.CalculateTargetOffset(
            selectedIndex: 6,
            itemCount: 7,
            laidOutRowTop: 0,
            laidOutRowHeight: 0,
            laidOutContentHeight: 0,
            viewportHeight: 380,
            currentOffset: 0,
            fallbackRowHeight: 60,
            cellPadding: 5,
            verticalMargin: 5,
            edgeInset: 8);

        Assert.Equal(140, offset);
    }

    [Fact]
    public void VisibleSelectionKeepsCurrentOffset()
    {
        int offset = TrackListScrollCalculator.CalculateTargetOffset(
            selectedIndex: 2,
            itemCount: 7,
            laidOutRowTop: 155,
            laidOutRowHeight: 60,
            laidOutContentHeight: 520,
            viewportHeight: 380,
            currentOffset: 40,
            fallbackRowHeight: 60,
            cellPadding: 5,
            verticalMargin: 5,
            edgeInset: 8);

        Assert.Equal(40, offset);
    }

    [Fact]
    public void MovingUpRevealsSelectionWithInset()
    {
        int offset = TrackListScrollCalculator.CalculateTargetOffset(
            selectedIndex: 1,
            itemCount: 7,
            laidOutRowTop: 80,
            laidOutRowHeight: 60,
            laidOutContentHeight: 520,
            viewportHeight: 380,
            currentOffset: 120,
            fallbackRowHeight: 60,
            cellPadding: 5,
            verticalMargin: 5,
            edgeInset: 8);

        Assert.Equal(72, offset);
    }
}
