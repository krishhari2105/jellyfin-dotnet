using Xunit;
using Cache = JellyfinTizen.Utils.CacheHelper;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace JellyfinTizen.CacheHelper.Tests
{
    public sealed class CacheHelperTests : IDisposable
    {
        public CacheHelperTests()
        {
            Cache.Clear();
            Cache.MaxEntries = 500;
            Cache.MaxBytes = 0;
        }

        public void Dispose()
        {
            Cache.Clear();
            Cache.MaxEntries = 500;
            Cache.MaxBytes = 0;
        }

        [Fact]
        public async Task ByteLimitEvictsTheLeastRecentlyUsedAccountedEntry()
        {
            Cache.MaxBytes = 10;
            await AddAsync("a", "a", 5);
            await AddAsync("b", "b", 5);

            Assert.Equal("a", await AddAsync("a", "replacement", 5)); // Touch a.
            await AddAsync("c", "c", 5);

            int factoryCalls = 0;
            var reloaded = await Cache.GetOrAddAsync(
                "b",
                () =>
                {
                    factoryCalls++;
                    return Task.FromResult("b-reloaded");
                },
                TimeSpan.FromMinutes(1),
                _ => 5);

            Assert.Equal("b-reloaded", reloaded);
            Assert.Equal(1, factoryCalls);
            Assert.Equal(10, Cache.CurrentSizeBytes);
        }

        [Fact]
        public async Task ExpiredReplacementUpdatesAccountedSize()
        {
            await Cache.GetOrAddAsync("item", () => Task.FromResult("old"), TimeSpan.FromMilliseconds(-1), _ => 8);
            await Cache.GetOrAddAsync("item", () => Task.FromResult("new"), TimeSpan.FromMinutes(1), _ => 3);

            Assert.Equal(3, Cache.CurrentSizeBytes);
        }

        [Fact]
        public async Task OversizedItemIsNotRetainedOrAllowedToFlushExistingCache()
        {
            Cache.MaxBytes = 10;
            await AddAsync("kept", "kept", 6);
            await AddAsync("oversized", "oversized", 11);

            int factoryCalls = 0;
            var kept = await Cache.GetOrAddAsync(
                "kept",
                () =>
                {
                    factoryCalls++;
                    return Task.FromResult("unexpected");
                },
                TimeSpan.FromMinutes(1),
                _ => 6);

            Assert.Equal("kept", kept);
            Assert.Equal(0, factoryCalls);
            Assert.Equal(6, Cache.CurrentSizeBytes);
        }

        [Fact]
        public async Task ClearAndLimitReductionKeepByteTotalAccurate()
        {
            await AddAsync("a", "a", 4);
            await AddAsync("b", "b", 4);
            Assert.Equal(8, Cache.CurrentSizeBytes);

            Cache.MaxBytes = 4;
            Assert.Equal(4, Cache.CurrentSizeBytes);

            Cache.Clear();
            Assert.Equal(0, Cache.CurrentSizeBytes);
        }

        private static Task<string> AddAsync(string key, string value, long sizeBytes)
        {
            return Cache.GetOrAddAsync(key, () => Task.FromResult(value), TimeSpan.FromMinutes(1), _ => sizeBytes);
        }
    }
}
