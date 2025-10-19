namespace Caching
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Cache base class.
    /// </summary>
    public abstract class CacheBase<T1, T2>
    {
        #region Public-Members

        /// <summary>
        /// Cancellation token.
        /// </summary>
        public CancellationToken Token
        {
            get
            {
                return _Token;
            }
            set
            {
                _Token = value;
            }
        }

        /// <summary>
        /// Cache events.
        /// </summary>
        public CacheEvents<T1, T2> Events
        {
            get
            {
                return _Events;
            }
            set
            {
                if (value == null) _Events = new CacheEvents<T1, T2>();
                else _Events = value;
            }
        }

        /// <summary>
        /// Persistence driver.
        /// </summary>
        public IPersistenceDriver<T1, T2> Persistence
        {
            get
            {
                return _Persistence;
            }
            set
            {
                _Persistence = value;
            }
        }

        /// <summary>
        /// Async persistence driver (optional).
        /// </summary>
        public IPersistenceDriverAsync<T1, T2> PersistenceAsync { get; set; }

        /// <summary>
        /// Cache capacity.
        /// </summary>
        public int Capacity { get; internal set; } = 0;

        /// <summary>
        /// Number of entries to evict when cache reaches capacity.
        /// </summary>
        public int EvictCount { get; internal set; } = 0;

        /// <summary>
        /// Frequency with which the cache is evaluated for expired entries.  Default is 1000ms.
        /// </summary>
        public int ExpirationIntervalMs
        {
            get
            {
                return _ExpirationIntervalMs;
            }
            set
            {
                if (value < 1) throw new ArgumentException("ExpirationIntervalMs must be at least 1ms.");
                _ExpirationIntervalMs = value;
            }
        }

        /// <summary>
        /// If true, accessing an item refreshes its expiration time.
        /// </summary>
        public bool SlidingExpiration { get; set; } = false;

        /// <summary>
        /// Maximum memory limit in bytes (0 = no limit).
        /// </summary>
        public long MaxMemoryBytes { get; set; } = 0;

        /// <summary>
        /// Current estimated memory usage in bytes.
        /// </summary>
        public long CurrentMemoryBytes { get; protected set; } = 0;

        /// <summary>
        /// Function to estimate size of values.
        /// </summary>
        public Func<T2, long> SizeEstimator { get; set; }

        /// <summary>
        /// Total number of cache hits.
        /// </summary>
        public long HitCount => _hitCount;

        /// <summary>
        /// Total number of cache misses.
        /// </summary>
        public long MissCount => _missCount;

        /// <summary>
        /// Total number of evicted entries.
        /// </summary>
        public long EvictionCount => _evictionCount;

        /// <summary>
        /// Total number of expired entries.
        /// </summary>
        public long ExpirationCount => _expirationCount;

        /// <summary>
        /// Cache hit rate (0.0 to 1.0).
        /// </summary>
        public double HitRate
        {
            get
            {
                long total = _hitCount + _missCount;
                return total == 0 ? 0.0 : (double)_hitCount / total;
            }
        }

        #endregion

        #region Internal-Members

        internal CancellationTokenSource _TokenSource = new CancellationTokenSource();
        internal CancellationToken _Token;

        internal int _ExpirationIntervalMs = 1000;
        internal readonly object _CacheLock = new object();
        internal Dictionary<T1, DataNode<T2>> _Cache = new Dictionary<T1, DataNode<T2>>();
        internal IPersistenceDriver<T1, T2> _Persistence = null;
        internal CacheEvents<T1, T2> _Events = new CacheEvents<T1, T2>();
        internal Task _ExpirationTaskInstance = null;
        internal bool _disposed = false;

        internal long _hitCount = 0;
        internal long _missCount = 0;
        internal long _evictionCount = 0;
        internal long _expirationCount = 0;

        internal abstract Task ExpirationTask(CancellationToken token = default);

        #endregion

        #region Constructors-and-Factories

        #endregion

        #region Public-Methods

        /// <summary>
        /// Retrieve the current number of entries in the cache.
        /// </summary>
        /// <returns>An integer containing the number of entries.</returns>
        public abstract int Count();

        /// <summary>
        /// Retrieve the key of the oldest entry in the cache.
        /// </summary>
        /// <returns>String containing the key.</returns>
        public abstract T1 Oldest();

        /// <summary>
        /// Retrieve the key of the newest entry in the cache.
        /// </summary>
        /// <returns>String containing the key.</returns>
        public abstract T1 Newest();

        /// <summary>
        /// Retrieve all entries from the cache.
        /// </summary>
        /// <returns>Dictionary.</returns>
        public abstract Dictionary<T1, T2> All();

        /// <summary>
        /// Clear the cache.
        /// </summary>
        public abstract void Clear();

        /// <summary>
        /// Retrieve a key's value from the cache.
        /// </summary>
        /// <param name="key">The key associated with the data you wish to retrieve.</param>
        /// <returns>The object data associated with the key.</returns>
        public abstract T2 Get(T1 key);

        /// <summary>
        /// Retrieve a key's value from the cache.
        /// </summary>
        /// <param name="key">The key associated with the data you wish to retrieve.</param>
        /// <param name="val">The value associated with the key.</param>
        /// <returns>True if key is found.</returns>
        public abstract bool TryGet(T1 key, out T2 val);

        /// <summary>
        /// See if a key exists in the cache.
        /// </summary>
        /// <param name="key">The key of the cached items.</param>
        /// <returns>True if cached.</returns>
        public abstract bool Contains(T1 key);

        /// <summary>
        /// Add or replace a key's value in the cache.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="val">The value associated with the key.</param>
        /// <param name="expiration">Timestamp at which the entry should expire.</param>
        public abstract void AddReplace(T1 key, T2 val, DateTime? expiration = null);

        /// <summary>
        /// Add or replace a key's value in the cache with relative expiration time.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="val">The value associated with the key.</param>
        /// <param name="expiresIn">Time span until expiration.</param>
        public void AddReplace(T1 key, T2 val, TimeSpan expiresIn)
        {
            AddReplace(key, val, DateTime.UtcNow.Add(expiresIn));
        }

        /// <summary>
        /// Attempt to add or replace a key's value in the cache.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="val">The value associated with the key.</param>
        /// <param name="expiration">Timestamp at which the entry should expire.</param>
        /// <returns>True if successful.</returns>
        public abstract bool TryAddReplace(T1 key, T2 val, DateTime? expiration = null);

        /// <summary>
        /// Get a value from cache, or add it if not present.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="valueFactory">Function to create value if not present.</param>
        /// <param name="expiration">Timestamp at which the entry should expire.</param>
        /// <returns>The value.</returns>
        public abstract T2 GetOrAdd(T1 key, Func<T1, T2> valueFactory, DateTime? expiration = null);

        /// <summary>
        /// Get a value from cache, or add it if not present (with relative expiration).
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="valueFactory">Function to create value if not present.</param>
        /// <param name="expiresIn">Time span until expiration.</param>
        /// <returns>The value.</returns>
        public T2 GetOrAdd(T1 key, Func<T1, T2> valueFactory, TimeSpan? expiresIn)
        {
            DateTime? expiration = expiresIn.HasValue
                ? DateTime.UtcNow.Add(expiresIn.Value)
                : (DateTime?)null;
            return GetOrAdd(key, valueFactory, expiration);
        }

        /// <summary>
        /// Try to get a value from cache, or add it if not present.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="valueFactory">Function to create value if not present.</param>
        /// <param name="value">The retrieved or created value.</param>
        /// <param name="expiration">Timestamp at which the entry should expire.</param>
        /// <returns>True if successful.</returns>
        public abstract bool TryGetOrAdd(T1 key, Func<T1, T2> valueFactory, out T2 value, DateTime? expiration = null);

        /// <summary>
        /// Add a new value or update existing value.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="addValue">Value to add if key doesn't exist.</param>
        /// <param name="updateValueFactory">Function to update value if key exists.</param>
        /// <param name="expiration">Timestamp at which the entry should expire.</param>
        /// <returns>The resulting value.</returns>
        public abstract T2 AddOrUpdate(T1 key, T2 addValue, Func<T1, T2, T2> updateValueFactory, DateTime? expiration = null);

        /// <summary>
        /// Remove a key from the cache.
        /// </summary>
        /// <param name="key">The key.</param>
        public abstract void Remove(T1 key);

        /// <summary>
        /// Attempt to remove a key and value value from the cache.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>True if successful.</returns>
        public abstract bool TryRemove(T1 key);

        /// <summary>
        /// Retrieve all keys in the cache.
        /// </summary>
        /// <returns>List of string.</returns>
        public abstract List<T1> GetKeys();

        /// <summary>
        /// Prepopulate the cache with entries from the persistence layer.
        /// </summary>
        public abstract void Prepopulate();

        /// <summary>
        /// Get current cache statistics.
        /// </summary>
        /// <returns>Cache statistics.</returns>
        public CacheStatistics GetStatistics()
        {
            return new CacheStatistics
            {
                HitCount = _hitCount,
                MissCount = _missCount,
                EvictionCount = _evictionCount,
                ExpirationCount = _expirationCount,
                HitRate = HitRate,
                CurrentCount = Count(),
                Capacity = Capacity,
                CurrentMemoryBytes = CurrentMemoryBytes
            };
        }

        /// <summary>
        /// Reset all statistics counters.
        /// </summary>
        public void ResetStatistics()
        {
            Interlocked.Exchange(ref _hitCount, 0);
            Interlocked.Exchange(ref _missCount, 0);
            Interlocked.Exchange(ref _evictionCount, 0);
            Interlocked.Exchange(ref _expirationCount, 0);
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Throw if disposed.
        /// </summary>
        protected void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        /// <summary>
        /// Estimate size of a value.
        /// </summary>
        /// <param name="value">Value to estimate.</param>
        /// <returns>Estimated size in bytes.</returns>
        protected long EstimateSize(T2 value)
        {
            if (SizeEstimator != null)
                return SizeEstimator(value);

            // Default estimations
            if (value is string str)
                return str.Length * 2; // Unicode chars

            if (value is byte[] bytes)
                return bytes.Length;

            return 0; // Unknown
        }

        #endregion

        #region Internal-Methods

        #endregion

        #region Private-Methods

        #endregion
    }

    /// <summary>
    /// Cache base class (deprecated name for backward compatibility).
    /// Use CacheBase instead.
    /// </summary>
    [Obsolete("Use CacheBase<T1, T2> instead. ICache will be removed in v5.0.")]
    public abstract class ICache<T1, T2> : CacheBase<T1, T2>
    {
    }
}
