using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JellyfinTizen.Utils
{
    /// <summary>
    /// Thread-safe in-memory cache with per-item TTL and LRU eviction.
    /// </summary>
    public static class CacheHelper
    {
        private sealed class CacheItem
        {
            public object Value;
            public DateTime Expiration;
        }

        private sealed class LruNode
        {
            public string Key;
            public LruNode Prev;
            public LruNode Next;
        }

        private const int DefaultMaxEntries = 500;

        private static readonly ConcurrentDictionary<string, CacheItem> _cache = new();
        private static readonly ConcurrentDictionary<string, LruNode> _nodes = new();
        private static readonly object _lruLock = new();
        private static LruNode _lruHead;
        private static LruNode _lruTail;
        private static int _maxEntries = DefaultMaxEntries;

        /// <summary>
        /// Gets or sets the maximum number of cache entries. When exceeded, LRU entries are evicted.
        /// Default is 500.
        /// </summary>
        public static int MaxEntries
        {
            get => _maxEntries;
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(value), "MaxEntries must be >= 1");
                _maxEntries = value;
                TrimToSize();
            }
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
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            var now = DateTime.UtcNow;
            if (_cache.TryGetValue(key, out var existing) && existing.Expiration > now)
            {
                Touch(key);
                return (T)existing.Value;
            }

            var value = await factory().ConfigureAwait(false);
            var item = new CacheItem { Value = value, Expiration = now.Add(duration) };
            _cache[key] = item;
            AddToLru(key);
            TrimToSize();
            return value;
        }

        /// <summary>
        /// Clears all cached items.
        /// </summary>
        public static void Clear()
        {
            _cache.Clear();
            lock (_lruLock)
            {
                _nodes.Clear();
                _lruHead = _lruTail = null;
            }
        }

        private static void Touch(string key)
        {
            if (_nodes.TryGetValue(key, out var node))
            {
                lock (_lruLock)
                {
                    MoveToFront(node);
                }
            }
        }

        private static void AddToLru(string key)
        {
            var node = new LruNode { Key = key };
            _nodes[key] = node;
            lock (_lruLock)
            {
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

        private static void TrimToSize()
        {
            while (_cache.Count > _maxEntries && _lruTail != null)
            {
                var tail = _lruTail;
                lock (_lruLock)
                {
                    if (_lruTail == tail)
                    {
                        _cache.TryRemove(tail.Key, out _);
                        _nodes.TryRemove(tail.Key, out _);
                        RemoveLruNode(tail);
                    }
                }
            }
        }
    }
}