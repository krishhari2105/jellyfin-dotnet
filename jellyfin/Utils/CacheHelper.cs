using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JellyfinTizen.Utils
{
    /// <summary>
    /// Thread-safe in-memory cache with per-item TTL and LRU eviction.
    /// Byte accounting is opt-in: callers provide a conservative logical or encoded
    /// size estimate when they know one. Object sizes are never guessed.
    /// </summary>
    public static class CacheHelper
    {
        private sealed class CacheItem
        {
            public object Value;
            public DateTime Expiration;
            public long SizeBytes;
        }

        private sealed class LruNode
        {
            public string Key;
            public LruNode Prev;
            public LruNode Next;
        }

        private const int DefaultMaxEntries = 500;

        private static readonly Dictionary<string, CacheItem> _cache = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, LruNode> _nodes = new(StringComparer.Ordinal);
        private static readonly object _cacheLock = new();
        private static LruNode _lruHead;
        private static LruNode _lruTail;
        private static int _maxEntries = DefaultMaxEntries;
        private static long _maxBytes;
        private static long _currentSizeBytes;

        /// <summary>
        /// Gets or sets the maximum number of cache entries. When exceeded, LRU entries are evicted.
        /// Default is 500.
        /// </summary>
        public static int MaxEntries
        {
            get { lock (_cacheLock) return _maxEntries; }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(value), "MaxEntries must be >= 1");
                lock (_cacheLock)
                {
                    _maxEntries = value;
                    TrimToLimitsLocked();
                }
            }
        }

        /// <summary>
        /// Gets or sets the maximum accounted cache size in bytes. A value of zero
        /// disables byte-based eviction. Existing callers without a size estimator
        /// account for zero bytes and retain their current behavior.
        /// </summary>
        public static long MaxBytes
        {
            get { lock (_cacheLock) return _maxBytes; }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "MaxBytes must be >= 0");
                lock (_cacheLock)
                {
                    _maxBytes = value;
                    TrimToLimitsLocked();
                }
            }
        }

        /// <summary>Gets the total caller-supplied byte estimate for retained items.</summary>
        public static long CurrentSizeBytes
        {
            get { lock (_cacheLock) return _currentSizeBytes; }
        }

        /// <summary>
        /// Retrieves a cached value or creates it via the supplied factory.
        /// </summary>
        /// <typeparam name="T">Type of the cached value.</typeparam>
        /// <param name="key">Unique cache key.</param>
        /// <param name="factory">Async factory to produce the value when missing or expired.</param>
        /// <param name="duration">How long the value should be cached.</param>
        /// <returns>Cached or newly created value.</returns>
        public static async Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> factory, TimeSpan duration)
        {
            return await GetOrAddAsync(key, factory, duration, sizeEstimator: null).ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieves a cached value or creates it via the supplied factory, with an
        /// optional caller-supplied size estimate used for byte-based eviction.
        /// The estimate must be non-negative and should describe known payload bytes
        /// (for example encoded response bytes), not an inferred managed-object size.
        /// </summary>
        public static async Task<T> GetOrAddAsync<T>(
            string key,
            Func<Task<T>> factory,
            TimeSpan duration,
            Func<T, long> sizeEstimator)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            var now = DateTime.UtcNow;
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(key, out var existing))
                {
                    if (existing.Expiration > now)
                    {
                        TouchLocked(key);
                        return (T)existing.Value;
                    }

                    RemoveEntryLocked(key);
                }
            }

            var value = await factory().ConfigureAwait(false);
            long sizeBytes = sizeEstimator?.Invoke(value) ?? 0;
            if (sizeBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(sizeEstimator), "Cache item size must be non-negative.");

            lock (_cacheLock)
            {
                // Preserve the existing cache's last-writer-wins behavior when
                // concurrent factories complete for the same key.
                RemoveEntryLocked(key);

                // An item that cannot fit on its own should not flush useful
                // entries only to evict itself. Leave it uncached instead.
                if (_maxBytes > 0 && sizeBytes > _maxBytes)
                    return value;

                MakeRoomForSizeLocked(sizeBytes);
                _cache[key] = new CacheItem
                {
                    Value = value,
                    Expiration = now.Add(duration),
                    SizeBytes = sizeBytes
                };
                _currentSizeBytes += sizeBytes;
                AddToLruLocked(key);
                TrimToLimitsLocked();
            }
            return value;
        }

        /// <summary>
        /// Clears all cached items.
        /// </summary>
        public static void Clear()
        {
            lock (_cacheLock)
            {
                _cache.Clear();
                _nodes.Clear();
                _lruHead = _lruTail = null;
                _currentSizeBytes = 0;
            }
        }

        private static void TouchLocked(string key)
        {
            if (_nodes.TryGetValue(key, out var node))
                MoveToFront(node);
        }

        private static void AddToLruLocked(string key)
        {
            if (_nodes.TryGetValue(key, out var existing))
                RemoveLruNode(existing);

            var node = new LruNode { Key = key };
            _nodes[key] = node;
            if (_lruHead == null)
            {
                _lruHead = _lruTail = node;
            }
            else
            {
                node.Next = _lruHead;
                _lruHead.Prev = node;
                _lruHead = node;
            }
        }

        private static void RemoveEntryLocked(string key)
        {
            if (_cache.Remove(key, out var item))
            {
                _currentSizeBytes -= item.SizeBytes;
            }

            if (_nodes.Remove(key, out var node))
                RemoveLruNode(node);
        }

        private static void MoveToFront(LruNode node)
        {
            if (node == _lruHead) return;

            // Unlink
            if (node.Prev != null) node.Prev.Next = node.Next;
            if (node.Next != null) node.Next.Prev = node.Prev;
            if (node == _lruTail) _lruTail = node.Prev;

            // Link at head
            node.Prev = null;
            node.Next = _lruHead;
            _lruHead.Prev = node;
            _lruHead = node;
        }

        private static void RemoveLruNode(LruNode node)
        {
            if (node.Prev != null) node.Prev.Next = node.Next;
            if (node.Next != null) node.Next.Prev = node.Prev;
            if (node == _lruHead) _lruHead = node.Next;
            if (node == _lruTail) _lruTail = node.Prev;
        }

        private static void MakeRoomForSizeLocked(long sizeBytes)
        {
            while (_currentSizeBytes > long.MaxValue - sizeBytes && _lruTail != null)
            {
                RemoveEntryLocked(_lruTail.Key);
            }
        }

        private static void TrimToLimitsLocked()
        {
            while (_lruTail != null &&
                (_cache.Count > _maxEntries || (_maxBytes > 0 && _currentSizeBytes > _maxBytes)))
            {
                RemoveEntryLocked(_lruTail.Key);
            }
        }
    }
}
