namespace Caching
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// FIFO cache - evicts oldest entries first based on insertion time.
    /// </summary>
    public class FIFOCache<T1, T2> : CacheBase<T1, T2>
    {
        #region Constructors-and-Factories

        /// <summary>
        /// Initialize the cache.
        /// </summary>
        /// <param name="capacity">Maximum number of entries.</param>
        /// <param name="evictCount">Number to evict when capacity is reached.</param>
        /// <param name="comparer">Optional equality comparer for keys.</param>
        public FIFOCache(int capacity, int evictCount, IEqualityComparer<T1> comparer = null)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (evictCount < 1) throw new ArgumentOutOfRangeException(nameof(evictCount));
            if (evictCount > capacity) throw new ArgumentOutOfRangeException(nameof(evictCount));

            Capacity = capacity;
            EvictCount = evictCount;
            _KeyComparer = comparer;

            _Cache = comparer != null
                ? new Dictionary<T1, DataNode<T2>>(comparer)
                : new Dictionary<T1, DataNode<T2>>();
            _Persistence = null;
            _Token = _TokenSource.Token;

            _ExpirationTaskInstance = Task.Run(() => ExpirationTask(_Token));
        }

        /// <summary>
        /// Initialize the cache with persistence.
        /// </summary>
        /// <param name="capacity">Maximum number of entries.</param>
        /// <param name="evictCount">Number to evict when capacity is reached.</param>
        /// <param name="persistence">Persistence driver.</param>
        /// <param name="comparer">Optional equality comparer for keys.</param>
        public FIFOCache(int capacity, int evictCount, IPersistenceDriver<T1, T2> persistence, IEqualityComparer<T1> comparer = null)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (evictCount < 1) throw new ArgumentOutOfRangeException(nameof(evictCount));
            if (evictCount > capacity) throw new ArgumentOutOfRangeException(nameof(evictCount));

            Capacity = capacity;
            EvictCount = evictCount;
            _KeyComparer = comparer;

            _Cache = comparer != null
                ? new Dictionary<T1, DataNode<T2>>(comparer)
                : new Dictionary<T1, DataNode<T2>>();
            _Persistence = persistence;
            _Token = _TokenSource.Token;

            _ExpirationTaskInstance = Task.Run(() => ExpirationTask(_Token));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public override int Count()
        {
            lock (_CacheLock)
            {
                return _Cache?.Count ?? 0;
            }
        }

        /// <inheritdoc />
        public override T1 Oldest()
        {
            lock (_CacheLock)
            {
                if (_Cache == null || _Cache.Count < 1) throw new KeyNotFoundException();

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

        /// <inheritdoc />
        public override T1 Newest()
        {
            lock (_CacheLock)
            {
                if (_Cache == null || _Cache.Count < 1) throw new KeyNotFoundException();

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

        /// <inheritdoc />
        public override Dictionary<T1, T2> All()
        {
            Dictionary<T1, T2> ret = _KeyComparer != null
                ? new Dictionary<T1, T2>(_KeyComparer)
                : new Dictionary<T1, T2>();
            Dictionary<T1, DataNode<T2>> dump = null;

            lock (_CacheLock)
            {
                if (_Cache == null) return ret;
                dump = new Dictionary<T1, DataNode<T2>>(_Cache);
            }

            foreach (KeyValuePair<T1, DataNode<T2>> cached in dump)
            {
                ret.Add(cached.Key, cached.Value.Data);
            }

            return ret;
        }

        /// <inheritdoc />
        public override void Clear()
        {
            ClearAsync().GetAwaiter().GetResult();
        }

        /// <inheritdoc />
        public override async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            lock (_CacheLock)
            {
                _Cache = _KeyComparer != null
                    ? new Dictionary<T1, DataNode<T2>>(_KeyComparer)
                    : new Dictionary<T1, DataNode<T2>>();
                CurrentMemoryBytes = 0;
            }

            if (_Persistence != null)
            {
                await _Persistence.ClearAsync(cancellationToken).ConfigureAwait(false);
            }

            _Events?.OnCleared(this, EventArgs.Empty);
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public override T2 GetOrDefault(T1 key, T2 defaultValue = default)
        {
            ThrowIfDisposed();
            if (key == null) throw new ArgumentNullException(nameof(key));

            lock (_CacheLock)
            {
                if (_Cache.TryGetValue(key, out DataNode<T2> node))
                {
                    Interlocked.Increment(ref _hitCount);
                    node.LastUsed = DateTime.UtcNow;

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
                    return defaultValue;
                }
            }
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public override bool Contains(T1 key)
        {
            ThrowIfDisposed();
            if (key == null) throw new ArgumentNullException(nameof(key));

            lock (_CacheLock)
            {
                return _Cache.ContainsKey(key);
            }
        }

        /// <inheritdoc />
        public override void AddReplace(T1 key, T2 val, DateTime? expiration = null)
        {
            AddReplaceAsync(key, val, expiration).GetAwaiter().GetResult();
        }

        /// <inheritdoc />
        public override async Task AddReplaceAsync(T1 key, T2 val, DateTime? expiration = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (key == null) throw new ArgumentNullException(nameof(key));

            if (expiration != null)
            {
                expiration = expiration.Value.ToUniversalTime();
                if (DateTime.UtcNow > expiration.Value)
                    throw new ArgumentException("The specified expiration timestamp is in the past.");
            }

            bool wasReplaced = false;
            DataNode<T2> previousNode = null;
            DataNode<T2> addedNode = null;
            List<T1> evictedKeys = null;

            lock (_CacheLock)
            {
                if (_Cache.ContainsKey(key))
                {
                    previousNode = _Cache[key];
                    _Cache.Remove(key);

                    if (MaxMemoryBytes > 0)
                    {
                        CurrentMemoryBytes -= EstimateSize(previousNode.Data);
                    }

                    wasReplaced = true;
                }

                // Capacity-based eviction
                if (_Cache.Count >= Capacity)
                {
                    var toEvict = _Cache.OrderBy(x => x.Value.Added)
                                        .Take(EvictCount)
                                        .Select(x => x.Key)
                                        .ToList();

                    foreach (T1 evictKey in toEvict)
                    {
                        if (_Cache.TryGetValue(evictKey, out DataNode<T2> evictNode))
                        {
                            _Cache.Remove(evictKey);

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

                // Memory-based eviction
                if (MaxMemoryBytes > 0)
                {
                    long valueSize = EstimateSize(val);

                    while (CurrentMemoryBytes + valueSize > MaxMemoryBytes && _Cache.Count > 0)
                    {
                        var toEvict = _Cache.OrderBy(x => x.Value.Added).First();
                        _Cache.Remove(toEvict.Key);

                        CurrentMemoryBytes -= EstimateSize(toEvict.Value.Data);

                        if (evictedKeys == null) evictedKeys = new List<T1>();
                        evictedKeys.Add(toEvict.Key);
                        Interlocked.Increment(ref _evictionCount);
                    }

                    CurrentMemoryBytes += valueSize;
                }

                addedNode = new DataNode<T2>(val, expiration);
                _Cache.Add(key, addedNode);
            }

            // Persistence and events outside the lock
            if (_Persistence != null)
            {
                await _Persistence.WriteAsync(key, val, cancellationToken).ConfigureAwait(false);
            }

            if (evictedKeys != null && evictedKeys.Count > 0)
            {
                if (_Persistence != null)
                {
                    foreach (T1 evictKey in evictedKeys)
                    {
                        await _Persistence.DeleteAsync(evictKey, cancellationToken).ConfigureAwait(false);
                    }
                }
                _Events?.OnEvicted(this, evictedKeys);
            }

            if (wasReplaced)
            {
                _Events?.OnReplaced(this, new DataEventArgs<T1, T2>(key, previousNode));
            }

            _Events?.OnAdded(this, new DataEventArgs<T1, T2>(key, addedNode));
        }

        /// <inheritdoc />
        public override bool TryAddReplace(T1 key, T2 val, DateTime? expiration = null)
        {
            try
            {
                AddReplace(key, val, expiration);
                return true;
            }
            catch (ArgumentNullException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <inheritdoc />
        public override T2 GetOrAdd(T1 key, Func<T1, T2> valueFactory, DateTime? expiration = null)
        {
            ThrowIfDisposed();
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (valueFactory == null) throw new ArgumentNullException(nameof(valueFactory));

            lock (_CacheLock)
            {
                if (_Cache.TryGetValue(key, out DataNode<T2> node))
                {
                    Interlocked.Increment(ref _hitCount);
                    node.LastUsed = DateTime.UtcNow;
                    return node.Data;
                }

                Interlocked.Increment(ref _missCount);
            }

            // Factory called outside lock to avoid blocking other operations
            T2 newValue = valueFactory(key);
            AddReplace(key, newValue, expiration);
            return newValue;
        }

        /// <inheritdoc />
        public override async Task<T2> GetOrAddAsync(T1 key, Func<T1, Task<T2>> valueFactory, DateTime? expiration = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (valueFactory == null) throw new ArgumentNullException(nameof(valueFactory));

            lock (_CacheLock)
            {
                if (_Cache.TryGetValue(key, out DataNode<T2> node))
                {
                    Interlocked.Increment(ref _hitCount);
                    node.LastUsed = DateTime.UtcNow;
                    return node.Data;
                }

                Interlocked.Increment(ref _missCount);
            }

            T2 newValue = await valueFactory(key).ConfigureAwait(false);
            await AddReplaceAsync(key, newValue, expiration, cancellationToken).ConfigureAwait(false);
            return newValue;
        }

        /// <inheritdoc />
        public override bool TryGetOrAdd(T1 key, Func<T1, T2> valueFactory, out T2 value, DateTime? expiration = null)
        {
            try
            {
                value = GetOrAdd(key, valueFactory, expiration);
                return true;
            }
            catch (ArgumentNullException)
            {
                value = default;
                return false;
            }
            catch (ArgumentException)
            {
                value = default;
                return false;
            }
        }

        /// <inheritdoc />
        public override T2 AddOrUpdate(T1 key, T2 addValue, Func<T1, T2, T2> updateValueFactory, DateTime? expiration = null)
        {
            ThrowIfDisposed();
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (updateValueFactory == null) throw new ArgumentNullException(nameof(updateValueFactory));

            T2 resultValue;

            lock (_CacheLock)
            {
                if (_Cache.TryGetValue(key, out DataNode<T2> existing))
                {
                    resultValue = updateValueFactory(key, existing.Data);
                }
                else
                {
                    resultValue = addValue;
                }
            }

            AddReplace(key, resultValue, expiration);
            return resultValue;
        }

        /// <inheritdoc />
        public override async Task<T2> AddOrUpdateAsync(T1 key, T2 addValue, Func<T1, T2, Task<T2>> updateValueFactory, DateTime? expiration = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (updateValueFactory == null) throw new ArgumentNullException(nameof(updateValueFactory));

            T2 resultValue;
            T2 existingValue = default;
            bool exists = false;

            lock (_CacheLock)
            {
                if (_Cache.TryGetValue(key, out DataNode<T2> existing))
                {
                    existingValue = existing.Data;
                    exists = true;
                }
            }

            if (exists)
            {
                resultValue = await updateValueFactory(key, existingValue).ConfigureAwait(false);
            }
            else
            {
                resultValue = addValue;
            }

            await AddReplaceAsync(key, resultValue, expiration, cancellationToken).ConfigureAwait(false);
            return resultValue;
        }

        /// <inheritdoc />
        public override void Remove(T1 key)
        {
            RemoveAsync(key).GetAwaiter().GetResult();
        }

        /// <inheritdoc />
        public override async Task RemoveAsync(T1 key, CancellationToken cancellationToken = default)
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

                    if (MaxMemoryBytes > 0)
                    {
                        CurrentMemoryBytes -= EstimateSize(val.Data);
                    }

                    existed = true;
                }
            }

            if (existed)
            {
                if (_Persistence != null)
                {
                    await _Persistence.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
                }
                _Events?.OnRemoved(this, new DataEventArgs<T1, T2>(key, val));
            }
        }

        /// <inheritdoc />
        public override bool TryRemove(T1 key, out T2 val)
        {
            if (key == null)
            {
                val = default;
                return false;
            }

            DataNode<T2> node = null;
            bool existed = false;

            lock (_CacheLock)
            {
                if (_Cache.ContainsKey(key))
                {
                    node = _Cache[key];
                    _Cache.Remove(key);

                    if (MaxMemoryBytes > 0)
                    {
                        CurrentMemoryBytes -= EstimateSize(node.Data);
                    }

                    existed = true;
                }
            }

            if (existed)
            {
                _Persistence?.DeleteAsync(key).GetAwaiter().GetResult();
                _Events?.OnRemoved(this, new DataEventArgs<T1, T2>(key, node));
                val = node.Data;
                return true;
            }

            val = default;
            return false;
        }

        /// <inheritdoc />
        public override List<T1> GetKeys()
        {
            lock (_CacheLock)
            {
                if (_Cache == null) return new List<T1>();
                return new List<T1>(_Cache.Keys);
            }
        }

        /// <inheritdoc />
        public override void Prepopulate()
        {
            PrepopulateAsync().GetAwaiter().GetResult();
        }

        /// <inheritdoc />
        public override async Task PrepopulateAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (_Persistence == null)
                throw new InvalidOperationException("No persistence driver has been defined for the cache.");

            List<T1> keys = await _Persistence.EnumerateAsync(cancellationToken).ConfigureAwait(false);

            if (keys != null && keys.Count > 0)
            {
                int loadCount = Math.Min(keys.Count, Capacity);

                for (int i = 0; i < loadCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    T1 key = keys[i];
                    T2 data = await _Persistence.GetAsync(key, cancellationToken).ConfigureAwait(false);
                    DataNode<T2> node = new DataNode<T2>(data);

                    bool added = false;

                    lock (_CacheLock)
                    {
                        if (_Cache.Count < Capacity)
                        {
                            // Race condition protection: check if key already exists
                            if (!_Cache.ContainsKey(key))
                            {
                                _Cache.Add(key, node);
                                added = true;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (added)
                    {
                        _Events?.OnPrepopulated(this, new DataEventArgs<T1, T2>(key, node));
                    }
                }
            }
        }

        #endregion

        #region Internal-Methods

        /// <inheritdoc />
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
                        if (_Cache == null) continue;

                        expired = _Cache.Where(
                            c => c.Value.Expiration != null && c.Value.Expiration.Value < DateTime.UtcNow)
                            .ToList();

                        if (expired != null && expired.Count > 0)
                        {
                            foreach (KeyValuePair<T1, DataNode<T2>> entry in expired)
                            {
                                _Cache.Remove(entry.Key);

                                if (MaxMemoryBytes > 0)
                                {
                                    CurrentMemoryBytes -= EstimateSize(entry.Value.Data);
                                }
                            }

                            Interlocked.Add(ref _expirationCount, expired.Count);
                        }
                    }

                    if (expired != null && expired.Count > 0)
                    {
                        foreach (KeyValuePair<T1, DataNode<T2>> entry in expired)
                        {
                            if (_Persistence != null)
                            {
                                try
                                {
                                    await _Persistence.DeleteAsync(entry.Key, token).ConfigureAwait(false);
                                }
                                catch (OperationCanceledException)
                                {
                                    break;
                                }
                            }
                            _Events?.OnExpired(this, entry.Key);
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Dispose of the object. Do not use after disposal.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _TokenSource.Cancel();

                try
                {
                    _ExpirationTaskInstance?.Wait(TimeSpan.FromSeconds(2));
                }
                catch (TaskCanceledException) { }
                catch (AggregateException) { }

                // Thread-safe disposal: hold lock for entire cleanup
                lock (_CacheLock)
                {
                    _Cache?.Clear();
                    _Cache = null;
                    CurrentMemoryBytes = 0;
                    Capacity = 0;
                    EvictCount = 0;
                    _disposed = true;
                }

                _Events?.OnDisposed(this, EventArgs.Empty);
                _Events = null;
                _Persistence = null;

                _TokenSource?.Dispose();
            }
        }

        #endregion
    }
}
