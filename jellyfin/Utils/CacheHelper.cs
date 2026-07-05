using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace JellyfinTizen.Utils
{
    /// <summary>
    /// Simple thread‑safe in‑memory cache with per‑item expiration.
    /// </summary>
    public static class CacheHelper
    {
        private class CacheItem
        {
            public object Value { get; set; }
            public DateTime Expiration { get; set; }
        }

        private static readonly ConcurrentDictionary<string, CacheItem> _cache = new();

        /// <summary>
        /// Retrieves a cached value or creates it via the supplied factory.
        /// </summary>
        /// <typeparam name="T">Type of the cached value.</typeparam>
        /// <param name="key">Unique cache key.</param>
        /// <param name="factory">Factory to produce the value when missing or expired.</param>
        /// <param name="duration">How long the value should be cached.</param>
        /// <returns>Cached or newly created value.</returns>
        public static async Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> factory, TimeSpan duration)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            var now = DateTime.UtcNow;
            if (_cache.TryGetValue(key, out var existing) && existing.Expiration > now)
            {
                return (T)existing.Value;
            }

            var value = await factory();
            var item = new CacheItem { Value = value, Expiration = now.Add(duration) };
            _cache[key] = item;
            return value;
        }

        /// <summary>
        /// Clears all cached items.
        /// </summary>
        public static void Clear()
        {
            _cache.Clear();
        }
    }
}
