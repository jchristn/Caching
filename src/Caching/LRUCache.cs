namespace Caching
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// LRU cache that internally uses tuples.  T1 is the type of the key, and T2 is the type of the value.
    /// </summary>
    public class LRUCache<T1, T2> : CacheBase<T1, T2>, IDisposable
    {
        #region Public-Members

        #endregion

        #region Internal-Members

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initialize the cache.
        /// </summary>
        /// <param name="capacity">Maximum number of entries.</param>
        /// <param name="evictCount">Number to evict when capacity is reached.</param>
        public LRUCache(int capacity, int evictCount)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (evictCount < 1) throw new ArgumentOutOfRangeException(nameof(evictCount));
            if (evictCount > capacity) throw new ArgumentOutOfRangeException(nameof(evictCount));

            Capacity = capacity;
            EvictCount = evictCount;
            _Cache = new Dictionary<T1, DataNode<T2>>();
            _Persistence = null;
            _Token = _TokenSource.Token;

            _ExpirationTaskInstance = Task.Run(() => ExpirationTask(_Token));
        }

        /// <summary>
        /// Initialize the cache.
        /// </summary>
        /// <param name="capacity">Maximum number of entries.</param>
        /// <param name="evictCount">Number to evict when capacity is reached.</param>
        /// <param name="persistence">Persistence driver.</param>
        public LRUCache(int capacity, int evictCount, IPersistenceDriver<T1, T2> persistence)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (evictCount < 1) throw new ArgumentOutOfRangeException(nameof(evictCount));
            if (evictCount > capacity) throw new ArgumentOutOfRangeException(nameof(evictCount));

            Capacity = capacity;
            EvictCount = evictCount;

            _Cache = new Dictionary<T1, DataNode<T2>>();
            _Persistence = persistence;
            _Token = _TokenSource.Token;

            _ExpirationTaskInstance = Task.Run(() => ExpirationTask(_Token));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Dispose of the object.  Do not use after disposal.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Retrieve the current number of entries in the cache.
        /// </summary>
        /// <returns>An integer containing the number of entries.</returns>
        public override int Count()
        {
            lock (_CacheLock)
            {
                return _Cache.Count;
            }
        }

        /// <summary>
        /// Retrieve the key of the oldest entry in the cache (by Added time).
        /// </summary>
        /// <returns>String containing the key.</returns>
        public override T1 Oldest()
        {
            lock (_CacheLock)
            {
                if (_Cache == null || _Cache.Count < 1) throw new KeyNotFoundException();

                // O(n) scan for minimum
                T1 oldestKey = default;
                DateTime oldestTime = DateTime.MaxValue;

                foreach (var kvp in _Cache)
                {
                    if (kvp.Value.Added < oldestTime)
                    {
                        oldestTime = kvp.Value.Added;
                        oldestKey = kvp.Key;
                    }
                }

                return oldestKey;
            }
        }

        /// <summary>
        /// Retrieve the key of the newest entry in the cache (by Added time).
        /// </summary>
        /// <returns>String containing the key.</returns>
        public override T1 Newest()
        {
            lock (_CacheLock)
            {
                if (_Cache == null || _Cache.Count < 1) throw new KeyNotFoundException();

                // O(n) scan for maximum
                T1 newestKey = default;
                DateTime newestTime = DateTime.MinValue;

                foreach (var kvp in _Cache)
                {
                    if (kvp.Value.Added > newestTime)
                    {
                        newestTime = kvp.Value.Added;
                        newestKey = kvp.Key;
                    }
                }

                return newestKey;
            }
        }

        /// <summary>
        /// Retrieve all entries from the cache.
        /// </summary>
        /// <returns>Dictionary.</returns>
        public override Dictionary<T1, T2> All()
        {
            Dictionary<T1, T2> ret = new Dictionary<T1, T2>();
            Dictionary<T1, DataNode<T2>> dump = null;

            lock (_CacheLock)
            {
                dump = new Dictionary<T1, DataNode<T2>>(_Cache);
            }

            foreach (KeyValuePair<T1, DataNode<T2>> cached in dump)
            {
                ret.Add(cached.Key, cached.Value.Data);
            }

            return ret;
        }

        /// <summary>
        /// Clear the cache.
        /// </summary>
        public override void Clear()
        {
            ThrowIfDisposed();

            lock (_CacheLock)
            {
                _Persistence?.Clear();
                _Cache = new Dictionary<T1, DataNode<T2>>();
                CurrentMemoryBytes = 0;
            }

            _Events?.OnCleared(this, EventArgs.Empty);
        }

        /// <summary>
        /// Retrieve a key's value from the cache.
        /// </summary>
        /// <param name="key">The key associated with the data you wish to retrieve.</param>
        /// <returns>The object data associated with the key.</returns>
        public override T2 Get(T1 key)
        {
            ThrowIfDisposed();
            if (key == null) throw new ArgumentNullException(nameof(key));

            lock (_CacheLock)
            {
                if (_Cache.TryGetValue(key, out DataNode<T2> node))
                {
                    Interlocked.Increment(ref _hitCount);

                    node.LastUsed = DateTime.UtcNow;

                    // Sliding expiration
                    if (SlidingExpiration && node.Expiration.HasValue)
                    {
                        TimeSpan timeToLive = node.Expiration.Value - node.Added;
                        node.Expiration = DateTime.UtcNow.Add(timeToLive);
                    }

                    return node.Data;
                }
                else
                {
                    Interlocked.Increment(ref _missCount);
                    throw new KeyNotFoundException();
                }
            }
        }

        /// <summary>
        /// Retrieve a key's value from the cache.
        /// </summary>
        /// <param name="key">The key associated with the data you wish to retrieve.</param>
        /// <param name="val">The value associated with the key.</param>
        /// <returns>True if key is found.</returns>
        public override bool TryGet(T1 key, out T2 val)
        {
            ThrowIfDisposed();
            if (key == null) throw new ArgumentNullException(nameof(key));

            lock (_CacheLock)
            {
                if (_Cache.TryGetValue(key, out DataNode<T2> node))
                {
                    Interlocked.Increment(ref _hitCount);

                    node.LastUsed = DateTime.UtcNow;

                    // Sliding expiration
                    if (SlidingExpiration && node.Expiration.HasValue)
                    {
                        TimeSpan timeToLive = node.Expiration.Value - node.Added;
                        node.Expiration = DateTime.UtcNow.Add(timeToLive);
                    }

                    val = node.Data;
                    return true;
                }
                else
                {
                    Interlocked.Increment(ref _missCount);
                    val = default;
                    return false;
                }
            }
        }

        /// <summary>
        /// See if a key exists in the cache.
        /// </summary>
        /// <param name="key">The key of the cached items.</param>
        /// <returns>True if cached.</returns>
        public override bool Contains(T1 key)
        {
            ThrowIfDisposed();
            if (key == null) throw new ArgumentNullException(nameof(key));

            lock (_CacheLock)
            {
                return _Cache.ContainsKey(key);
            }
        }

        /// <summary>
        /// Add or replace a key's value in the cache.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="val">The value associated with the key.</param>
        /// <param name="expiration">Timestamp at which the entry should expire.</param>
        /// <returns>Boolean indicating success.</returns>
        public override void AddReplace(T1 key, T2 val, DateTime? expiration = null)
        {
            ThrowIfDisposed();
            if (key == null) throw new ArgumentNullException(nameof(key));

            if (expiration != null)
            {
                expiration = expiration.Value.ToUniversalTime();
                if (DateTime.UtcNow > expiration.Value)
                    throw new ArgumentException("The specified expiration timestamp is in the past.");
            }

            // Capture data for outside-lock operations
            bool shouldWrite = false;
            T2 valueToWrite = default;
            DataNode<T2> previousNode = null;
            bool wasReplaced = false;
            DataNode<T2> addedNode = null;
            List<T1> evictedKeys = null;

            lock (_CacheLock)
            {
                bool replaced = false;
                DataNode<T2> previous = null;

                if (_Cache.ContainsKey(key))
                {
                    previous = _Cache[key];
                    _Cache.Remove(key);

                    // Update memory tracking
                    if (MaxMemoryBytes > 0)
                    {
                        CurrentMemoryBytes -= EstimateSize(previous.Data);
                    }

                    replaced = true;
                }

                // Check capacity and evict if needed (LRU evicts by LastUsed)
                if (_Cache.Count >= Capacity)
                {
                    var toEvict = _Cache.OrderBy(x => x.Value.LastUsed)
                                        .Take(EvictCount)
                                        .Select(x => x.Key)
                                        .ToList();

                    foreach (T1 evictKey in toEvict)
                    {
                        if (_Cache.TryGetValue(evictKey, out DataNode<T2> evictNode))
                        {
                            _Cache.Remove(evictKey);

                            // Update memory tracking
                            if (MaxMemoryBytes > 0)
                            {
                                CurrentMemoryBytes -= EstimateSize(evictNode.Data);
                            }
                        }
                    }

                    if (toEvict.Count > 0)
                    {
                        evictedKeys = toEvict;
                        Interlocked.Add(ref _evictionCount, toEvict.Count);
                    }
                }

                // Check memory limit and evict if needed
                if (MaxMemoryBytes > 0)
                {
                    long valueSize = EstimateSize(val);

                    while (CurrentMemoryBytes + valueSize > MaxMemoryBytes && _Cache.Count > 0)
                    {
                        var toEvict = _Cache.OrderBy(x => x.Value.LastUsed).First();
                        _Cache.Remove(toEvict.Key);

                        CurrentMemoryBytes -= EstimateSize(toEvict.Value.Data);

                        if (evictedKeys == null) evictedKeys = new List<T1>();
                        evictedKeys.Add(toEvict.Key);
                        Interlocked.Increment(ref _evictionCount);
                    }

                    CurrentMemoryBytes += valueSize;
                }

                DataNode<T2> curr = new DataNode<T2>(val, expiration);
                _Cache.Add(key, curr);

                // Capture for outside lock
                shouldWrite = true;
                valueToWrite = val;
                previousNode = previous;
                wasReplaced = replaced;
                addedNode = curr;
            }

            // Execute I/O and events outside the lock
            if (shouldWrite)
            {
                _Persistence?.Write(key, valueToWrite);
            }

            if (evictedKeys != null && evictedKeys.Count > 0)
            {
                foreach (T1 evictKey in evictedKeys)
                {
                    _Persistence?.Delete(evictKey);
                }
                _Events?.OnEvicted(this, evictedKeys);
            }

            if (wasReplaced)
            {
                _Events?.OnReplaced(this, new DataEventArgs<T1, T2>(key, previousNode));
            }

            _Events?.OnAdded(this, new DataEventArgs<T1, T2>(key, addedNode));
        }

        /// <summary>
        /// Attempt to add or replace a key's value in the cache.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="val">The value associated with the key.</param>
        /// <param name="expiration">Timestamp at which the entry should expire.</param>
        /// <returns>True if successful.</returns>
        public override bool TryAddReplace(T1 key, T2 val, DateTime? expiration = null)
        {
            try
            {
                AddReplace(key, val, expiration);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Get a value from cache, or add it if not present.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="valueFactory">Function to create value if not present.</param>
        /// <param name="expiration">Timestamp at which the entry should expire.</param>
        /// <returns>The value.</returns>
        public override T2 GetOrAdd(T1 key, Func<T1, T2> valueFactory, DateTime? expiration = null)
        {
            ThrowIfDisposed();
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (valueFactory == null) throw new ArgumentNullException(nameof(valueFactory));

            lock (_CacheLock)
            {
                // Try to get existing
                if (_Cache.TryGetValue(key, out DataNode<T2> node))
                {
                    Interlocked.Increment(ref _hitCount);
                    node.LastUsed = DateTime.UtcNow;
                    return node.Data;
                }

                Interlocked.Increment(ref _missCount);

                // Not found - create new value
                T2 newValue = valueFactory(key);

                // Add to cache using existing AddReplace logic
                AddReplace(key, newValue, expiration);

                return newValue;
            }
        }

        /// <summary>
        /// Try to get a value from cache, or add it if not present.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="valueFactory">Function to create value if not present.</param>
        /// <param name="value">The retrieved or created value.</param>
        /// <param name="expiration">Timestamp at which the entry should expire.</param>
        /// <returns>True if successful.</returns>
        public override bool TryGetOrAdd(T1 key, Func<T1, T2> valueFactory, out T2 value, DateTime? expiration = null)
        {
            try
            {
                value = GetOrAdd(key, valueFactory, expiration);
                return true;
            }
            catch (Exception)
            {
                value = default;
                return false;
            }
        }

        /// <summary>
        /// Add a new value or update existing value.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="addValue">Value to add if key doesn't exist.</param>
        /// <param name="updateValueFactory">Function to update value if key exists.</param>
        /// <param name="expiration">Timestamp at which the entry should expire.</param>
        /// <returns>The resulting value.</returns>
        public override T2 AddOrUpdate(T1 key, T2 addValue, Func<T1, T2, T2> updateValueFactory, DateTime? expiration = null)
        {
            ThrowIfDisposed();
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (updateValueFactory == null) throw new ArgumentNullException(nameof(updateValueFactory));

            lock (_CacheLock)
            {
                T2 resultValue;

                if (_Cache.TryGetValue(key, out DataNode<T2> existing))
                {
                    // Update existing
                    resultValue = updateValueFactory(key, existing.Data);
                }
                else
                {
                    // Add new
                    resultValue = addValue;
                }

                AddReplace(key, resultValue, expiration);
                return resultValue;
            }
        }

        /// <summary>
        /// Remove a key from the cache.
        /// </summary>
        /// <param name="key">The key.</param>
        public override void Remove(T1 key)
        {
            ThrowIfDisposed();
            if (key == null) throw new ArgumentNullException(nameof(key));

            DataNode<T2> val = null;
            bool existed = false;

            lock (_CacheLock)
            {
                if (_Cache.ContainsKey(key))
                {
                    val = _Cache[key];
                    _Cache.Remove(key);

                    // Update memory tracking
                    if (MaxMemoryBytes > 0)
                    {
                        CurrentMemoryBytes -= EstimateSize(val.Data);
                    }

                    existed = true;
                }
            }

            // Execute I/O outside lock
            if (existed)
            {
                _Persistence?.Delete(key);
                _Events?.OnRemoved(this, new DataEventArgs<T1, T2>(key, val));
            }
        }

        /// <summary>
        /// Attempt to remove a key and value value from the cache.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>True if successful.</returns>
        public override bool TryRemove(T1 key)
        {
            if (key == null) return false;

            bool existed = false;
            DataNode<T2> val = null;

            lock (_CacheLock)
            {
                if (_Cache.ContainsKey(key))
                {
                    val = _Cache[key];
                    _Cache.Remove(key);

                    // Update memory tracking
                    if (MaxMemoryBytes > 0)
                    {
                        CurrentMemoryBytes -= EstimateSize(val.Data);
                    }

                    existed = true;
                }
            }

            if (existed)
            {
                _Persistence?.Delete(key);
                _Events?.OnRemoved(this, new DataEventArgs<T1, T2>(key, val));
            }

            return existed;
        }

        /// <summary>
        /// Retrieve all keys in the cache.
        /// </summary>
        /// <returns>List of string.</returns>
        public override List<T1> GetKeys()
        {
            lock (_CacheLock)
            {
                List<T1> keys = new List<T1>(_Cache.Keys);
                return keys;
            }
        }

        /// <summary>
        /// Prepopulate the cache with entries from the persistence layer.
        /// </summary>
        public override void Prepopulate()
        {
            ThrowIfDisposed();
            if (_Persistence == null)
                throw new InvalidOperationException("No persistence driver has been defined for the cache.");

            List<T1> keys = _Persistence.Enumerate();

            if (keys != null && keys.Count > 0)
            {
                // Limit to capacity
                int loadCount = Math.Min(keys.Count, Capacity);

                for (int i = 0; i < loadCount; i++)
                {
                    T1 key = keys[i];
                    T2 data = _Persistence.Get(key);  // I/O outside lock
                    DataNode<T2> node = new DataNode<T2>(data);

                    lock (_CacheLock)
                    {
                        if (_Cache.Count < Capacity)
                        {
                            _Cache.Add(key, node);
                        }
                        else
                        {
                            break;
                        }
                    }

                    _Events?.OnPrepopulated(this, new DataEventArgs<T1, T2>(key, node));
                }
            }
        }

        #endregion

        #region Internal-Methods

        /// <summary>
        /// Expiration task.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        internal override async Task ExpirationTask(CancellationToken token = default)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_ExpirationIntervalMs, token).ConfigureAwait(false);

                    List<KeyValuePair<T1, DataNode<T2>>> expired = null;

                    lock (_CacheLock)
                    {
                        expired = _Cache.Where(
                            c => c.Value.Expiration != null && c.Value.Expiration.Value < DateTime.UtcNow)
                            .ToList();

                        if (expired != null && expired.Count > 0)
                        {
                            foreach (KeyValuePair<T1, DataNode<T2>> entry in expired)
                            {
                                _Cache.Remove(entry.Key);

                                // Update memory tracking
                                if (MaxMemoryBytes > 0)
                                {
                                    CurrentMemoryBytes -= EstimateSize(entry.Value.Data);
                                }
                            }

                            Interlocked.Add(ref _expirationCount, expired.Count);
                        }
                    }

                    // Execute I/O and events outside lock
                    if (expired != null && expired.Count > 0)
                    {
                        foreach (KeyValuePair<T1, DataNode<T2>> entry in expired)
                        {
                            _Persistence?.Delete(entry.Key);
                            _Events?.OnExpired(this, entry.Key);
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    // Expected when cancelling
                    break;
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancelling
                    break;
                }
            }
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Dispose of the object.  Do not use after disposal.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Cancel the background task
                _TokenSource.Cancel();

                // Wait for task to complete
                try
                {
                    _ExpirationTaskInstance?.Wait(TimeSpan.FromSeconds(2));
                }
                catch (TaskCanceledException) { }
                catch (AggregateException) { }

                lock (_CacheLock)
                {
                    _Cache?.Clear();
                    _Cache = null;
                }

                Capacity = 0;
                EvictCount = 0;

                _Events?.OnDisposed(this, EventArgs.Empty);
                _Events = null;
                _Persistence = null;

                _TokenSource?.Dispose();
                _disposed = true;
            }
        }

        #endregion
    }
}
