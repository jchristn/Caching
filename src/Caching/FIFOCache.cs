namespace Caching
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
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
            RegisterTelemetry();
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
            RegisterTelemetry();
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
            ThrowIfDisposed();

            lock (_CacheLock)
            {
                return _Cache?.Count ?? 0;
            }
        }

        /// <inheritdoc />
        public override T1 Oldest()
        {
            ThrowIfDisposed();

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
            ThrowIfDisposed();

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
            ThrowIfDisposed();

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
            long start = OperationTimestamp();
            Activity activity = StartOperationActivity(CacheTelemetryNames.OperationClear);

            try
            {
                await ClearCoreAsync(cancellationToken).ConfigureAwait(false);
                CompleteOperation(CacheTelemetryNames.OperationClear, start, CacheTelemetryNames.OutcomeSuccess, activity);
            }
            catch (Exception e) when (FailOperation(CacheTelemetryNames.OperationClear, start, e, activity))
            {
                throw;
            }
            finally
            {
                activity?.Dispose();
            }
        }

        private async Task ClearCoreAsync(CancellationToken cancellationToken)
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
                await InvokePersistenceAsync(CacheTelemetryNames.PersistenceClear, p => p.ClearAsync(cancellationToken)).ConfigureAwait(false);
            }

            _Events?.OnCleared(this, EventArgs.Empty);
        }

        /// <inheritdoc />
        public override T2 Get(T1 key)
        {
            long start = OperationTimestamp();
            Activity activity = StartOperationActivity(CacheTelemetryNames.OperationGet, true);

            try
            {
                T2 ret = GetCore(key);
                CompleteOperation(CacheTelemetryNames.OperationGet, start, CacheTelemetryNames.OutcomeSuccess, activity);
                return ret;
            }
            catch (Exception e) when (FailOperation(CacheTelemetryNames.OperationGet, start, e, activity))
            {
                throw;
            }
            finally
            {
                activity?.Dispose();
            }
        }

        private T2 GetCore(T1 key)
        {
            ThrowIfDisposed();
            if (key == null) throw new ArgumentNullException(nameof(key));

            lock (_CacheLock)
            {
                if (_Cache.TryGetValue(key, out DataNode<T2> node))
                {
                    RecordHit();
                    MarkAccessed(node);

                    return node.Data;
                }
                else
                {
                    RecordMiss();
                    throw new KeyNotFoundException();
                }
            }
        }

        /// <inheritdoc />
        public override T2 GetOrDefault(T1 key, T2 defaultValue = default)
        {
            long start = OperationTimestamp();
            Activity activity = StartOperationActivity(CacheTelemetryNames.OperationGetOrDefault, true);

            try
            {
                T2 ret = GetOrDefaultCore(key, defaultValue, out bool hit);
                CompleteOperation(CacheTelemetryNames.OperationGetOrDefault, start, hit ? CacheTelemetryNames.OutcomeSuccess : CacheTelemetryNames.OutcomeMiss, activity);
                return ret;
            }
            catch (Exception e) when (FailOperation(CacheTelemetryNames.OperationGetOrDefault, start, e, activity))
            {
                throw;
            }
            finally
            {
                activity?.Dispose();
            }
        }

        private T2 GetOrDefaultCore(T1 key, T2 defaultValue, out bool hit)
        {
            ThrowIfDisposed();
            if (key == null) throw new ArgumentNullException(nameof(key));

            lock (_CacheLock)
            {
                if (_Cache.TryGetValue(key, out DataNode<T2> node))
                {
                    RecordHit();
                    MarkAccessed(node);

                    hit = true;
                    return node.Data;
                }
                else
                {
                    RecordMiss();
                    hit = false;
                    return defaultValue;
                }
            }
        }

        /// <inheritdoc />
        public override bool TryGet(T1 key, out T2 val)
        {
            val = default;
            long start = OperationTimestamp();
            Activity activity = StartOperationActivity(CacheTelemetryNames.OperationTryGet, true);

            try
            {
                bool ret = TryGetCore(key, out val);
                CompleteOperation(CacheTelemetryNames.OperationTryGet, start, ret ? CacheTelemetryNames.OutcomeSuccess : CacheTelemetryNames.OutcomeMiss, activity);
                return ret;
            }
            catch (Exception e) when (FailOperation(CacheTelemetryNames.OperationTryGet, start, e, activity))
            {
                throw;
            }
            catch (Exception e) when (!(e is ObjectDisposedException))
            {
                // Try contract: report failure (already recorded above) as false instead of throwing. Disposal still throws.
                val = default;
                return false;
            }
            finally
            {
                activity?.Dispose();
            }
        }

        private bool TryGetCore(T1 key, out T2 val)
        {
            ThrowIfDisposed();
            if (key == null) throw new ArgumentNullException(nameof(key));

            lock (_CacheLock)
            {
                if (_Cache.TryGetValue(key, out DataNode<T2> node))
                {
                    RecordHit();
                    MarkAccessed(node);

                    val = node.Data;
                    return true;
                }
                else
                {
                    RecordMiss();
                    val = default;
                    return false;
                }
            }
        }

        /// <inheritdoc />
        public override bool Contains(T1 key)
        {
            long start = OperationTimestamp();
            Activity activity = StartOperationActivity(CacheTelemetryNames.OperationContains, true);

            try
            {
                bool ret = ContainsCore(key);
                CompleteOperation(CacheTelemetryNames.OperationContains, start, ret ? CacheTelemetryNames.OutcomeSuccess : CacheTelemetryNames.OutcomeMiss, activity);
                return ret;
            }
            catch (Exception e) when (FailOperation(CacheTelemetryNames.OperationContains, start, e, activity))
            {
                throw;
            }
            finally
            {
                activity?.Dispose();
            }
        }

        private bool ContainsCore(T1 key)
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
            long start = OperationTimestamp();
            Activity activity = StartOperationActivity(CacheTelemetryNames.OperationAddReplace);

            try
            {
                await AddReplaceCoreAsync(key, val, expiration, cancellationToken).ConfigureAwait(false);
                CompleteOperation(CacheTelemetryNames.OperationAddReplace, start, CacheTelemetryNames.OutcomeSuccess, activity);
            }
            catch (Exception e) when (FailOperation(CacheTelemetryNames.OperationAddReplace, start, e, activity))
            {
                throw;
            }
            finally
            {
                activity?.Dispose();
            }
        }

        private async Task AddReplaceCoreAsync(T1 key, T2 val, DateTime? expiration, CancellationToken cancellationToken)
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
                        RecordEvictions(toEvict.Count, CacheTelemetryNames.EvictionReasonCapacity);
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
                        RecordEvictions(1, CacheTelemetryNames.EvictionReasonMemory);
                    }

                    CurrentMemoryBytes += valueSize;
                }

                addedNode = new DataNode<T2>(val, expiration);
                _Cache.Add(key, addedNode);
            }

            // Persistence and events outside the lock
            if (_Persistence != null)
            {
                await InvokePersistenceAsync(CacheTelemetryNames.PersistenceWrite, p => p.WriteAsync(key, val, cancellationToken)).ConfigureAwait(false);
            }

            if (evictedKeys != null && evictedKeys.Count > 0)
            {
                if (_Persistence != null)
                {
                    foreach (T1 evictKey in evictedKeys)
                    {
                        await InvokePersistenceAsync(CacheTelemetryNames.PersistenceDelete, p => p.DeleteAsync(evictKey, cancellationToken)).ConfigureAwait(false);
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
            catch (Exception e) when (!(e is ObjectDisposedException))
            {
                // Try contract: report failure (recorded by AddReplace telemetry) as false instead of throwing. Disposal still throws.
                return false;
            }
        }

        /// <inheritdoc />
        public override T2 GetOrAdd(T1 key, Func<T1, T2> valueFactory, DateTime? expiration = null)
        {
            long start = OperationTimestamp();
            Activity activity = StartOperationActivity(CacheTelemetryNames.OperationGetOrAdd);

            try
            {
                T2 ret = GetOrAddCore(key, valueFactory, expiration);
                CompleteOperation(CacheTelemetryNames.OperationGetOrAdd, start, CacheTelemetryNames.OutcomeSuccess, activity);
                return ret;
            }
            catch (Exception e) when (FailOperation(CacheTelemetryNames.OperationGetOrAdd, start, e, activity))
            {
                throw;
            }
            finally
            {
                activity?.Dispose();
            }
        }

        private T2 GetOrAddCore(T1 key, Func<T1, T2> valueFactory, DateTime? expiration)
        {
            ThrowIfDisposed();
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (valueFactory == null) throw new ArgumentNullException(nameof(valueFactory));

            WaitAtomicLock(CacheTelemetryNames.OperationGetOrAdd);
            try
            {
                lock (_CacheLock)
                {
                    if (_Cache.TryGetValue(key, out DataNode<T2> node))
                    {
                        RecordHit();
                        MarkAccessed(node);
                        return node.Data;
                    }

                    RecordMiss();
                }

                // The factory, persistence write, and events run outside the cache lock so they can neither block
                // other cache users nor deadlock against them. The atomic lock still serializes GetOrAdd/AddOrUpdate.
                T2 newValue = InvokeValueFactory(() => valueFactory(key));
                AddReplace(key, newValue, expiration);
                return newValue;
            }
            finally
            {
                _AtomicLock.Release();
            }
        }

        /// <inheritdoc />
        public override async Task<T2> GetOrAddAsync(T1 key, Func<T1, Task<T2>> valueFactory, DateTime? expiration = null, CancellationToken cancellationToken = default)
        {
            long start = OperationTimestamp();
            Activity activity = StartOperationActivity(CacheTelemetryNames.OperationGetOrAdd);

            try
            {
                T2 ret = await GetOrAddCoreAsync(key, valueFactory, expiration, cancellationToken).ConfigureAwait(false);
                CompleteOperation(CacheTelemetryNames.OperationGetOrAdd, start, CacheTelemetryNames.OutcomeSuccess, activity);
                return ret;
            }
            catch (Exception e) when (FailOperation(CacheTelemetryNames.OperationGetOrAdd, start, e, activity))
            {
                throw;
            }
            finally
            {
                activity?.Dispose();
            }
        }

        private async Task<T2> GetOrAddCoreAsync(T1 key, Func<T1, Task<T2>> valueFactory, DateTime? expiration, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (valueFactory == null) throw new ArgumentNullException(nameof(valueFactory));

            await WaitAtomicLockAsync(CacheTelemetryNames.OperationGetOrAdd, cancellationToken).ConfigureAwait(false);
            try
            {
                lock (_CacheLock)
                {
                    if (_Cache.TryGetValue(key, out DataNode<T2> node))
                    {
                        RecordHit();
                        MarkAccessed(node);
                        return node.Data;
                    }

                    RecordMiss();
                }

                T2 newValue = await InvokeValueFactoryAsync(() => valueFactory(key)).ConfigureAwait(false);
                await AddReplaceAsync(key, newValue, expiration, cancellationToken).ConfigureAwait(false);
                return newValue;
            }
            finally
            {
                _AtomicLock.Release();
            }
        }

        /// <inheritdoc />
        public override bool TryGetOrAdd(T1 key, Func<T1, T2> valueFactory, out T2 value, DateTime? expiration = null)
        {
            try
            {
                value = GetOrAdd(key, valueFactory, expiration);
                return true;
            }
            catch (Exception e) when (!(e is ObjectDisposedException))
            {
                // Try contract: report failure (recorded by GetOrAdd telemetry) as false instead of throwing. Disposal still throws.
                value = default;
                return false;
            }
        }

        /// <inheritdoc />
        public override T2 AddOrUpdate(T1 key, T2 addValue, Func<T1, T2, T2> updateValueFactory, DateTime? expiration = null)
        {
            long start = OperationTimestamp();
            Activity activity = StartOperationActivity(CacheTelemetryNames.OperationAddOrUpdate);

            try
            {
                T2 ret = AddOrUpdateCore(key, addValue, updateValueFactory, expiration);
                CompleteOperation(CacheTelemetryNames.OperationAddOrUpdate, start, CacheTelemetryNames.OutcomeSuccess, activity);
                return ret;
            }
            catch (Exception e) when (FailOperation(CacheTelemetryNames.OperationAddOrUpdate, start, e, activity))
            {
                throw;
            }
            finally
            {
                activity?.Dispose();
            }
        }

        private T2 AddOrUpdateCore(T1 key, T2 addValue, Func<T1, T2, T2> updateValueFactory, DateTime? expiration)
        {
            ThrowIfDisposed();
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (updateValueFactory == null) throw new ArgumentNullException(nameof(updateValueFactory));

            WaitAtomicLock(CacheTelemetryNames.OperationAddOrUpdate);
            try
            {
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

                // The factory, persistence write, and events run outside the cache lock so they can neither block
                // other cache users nor deadlock against them. The atomic lock still serializes GetOrAdd/AddOrUpdate.
                if (exists)
                {
                    resultValue = InvokeValueFactory(() => updateValueFactory(key, existingValue));
                }
                else
                {
                    resultValue = addValue;
                }

                AddReplace(key, resultValue, expiration);
                return resultValue;
            }
            finally
            {
                _AtomicLock.Release();
            }
        }

        /// <inheritdoc />
        public override async Task<T2> AddOrUpdateAsync(T1 key, T2 addValue, Func<T1, T2, Task<T2>> updateValueFactory, DateTime? expiration = null, CancellationToken cancellationToken = default)
        {
            long start = OperationTimestamp();
            Activity activity = StartOperationActivity(CacheTelemetryNames.OperationAddOrUpdate);

            try
            {
                T2 ret = await AddOrUpdateCoreAsync(key, addValue, updateValueFactory, expiration, cancellationToken).ConfigureAwait(false);
                CompleteOperation(CacheTelemetryNames.OperationAddOrUpdate, start, CacheTelemetryNames.OutcomeSuccess, activity);
                return ret;
            }
            catch (Exception e) when (FailOperation(CacheTelemetryNames.OperationAddOrUpdate, start, e, activity))
            {
                throw;
            }
            finally
            {
                activity?.Dispose();
            }
        }

        private async Task<T2> AddOrUpdateCoreAsync(T1 key, T2 addValue, Func<T1, T2, Task<T2>> updateValueFactory, DateTime? expiration, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (updateValueFactory == null) throw new ArgumentNullException(nameof(updateValueFactory));

            await WaitAtomicLockAsync(CacheTelemetryNames.OperationAddOrUpdate, cancellationToken).ConfigureAwait(false);
            try
            {
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
                    resultValue = await InvokeValueFactoryAsync(() => updateValueFactory(key, existingValue)).ConfigureAwait(false);
                }
                else
                {
                    resultValue = addValue;
                }

                await AddReplaceAsync(key, resultValue, expiration, cancellationToken).ConfigureAwait(false);
                return resultValue;
            }
            finally
            {
                _AtomicLock.Release();
            }
        }

        /// <inheritdoc />
        public override void Remove(T1 key)
        {
            RemoveAsync(key).GetAwaiter().GetResult();
        }

        /// <inheritdoc />
        public override async Task RemoveAsync(T1 key, CancellationToken cancellationToken = default)
        {
            long start = OperationTimestamp();
            Activity activity = StartOperationActivity(CacheTelemetryNames.OperationRemove);

            try
            {
                bool ret = await RemoveCoreAsync(key, cancellationToken).ConfigureAwait(false);
                CompleteOperation(CacheTelemetryNames.OperationRemove, start, ret ? CacheTelemetryNames.OutcomeSuccess : CacheTelemetryNames.OutcomeMiss, activity);
            }
            catch (Exception e) when (FailOperation(CacheTelemetryNames.OperationRemove, start, e, activity))
            {
                throw;
            }
            finally
            {
                activity?.Dispose();
            }
        }

        private async Task<bool> RemoveCoreAsync(T1 key, CancellationToken cancellationToken)
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
                    await InvokePersistenceAsync(CacheTelemetryNames.PersistenceDelete, p => p.DeleteAsync(key, cancellationToken)).ConfigureAwait(false);
                }
                _Events?.OnRemoved(this, new DataEventArgs<T1, T2>(key, val));
            }

            return existed;
        }

        /// <inheritdoc />
        public override bool TryRemove(T1 key, out T2 val)
        {
            val = default;
            long start = OperationTimestamp();
            Activity activity = StartOperationActivity(CacheTelemetryNames.OperationTryRemove);

            try
            {
                bool ret = TryRemoveCore(key, out val);
                CompleteOperation(CacheTelemetryNames.OperationTryRemove, start, ret ? CacheTelemetryNames.OutcomeSuccess : CacheTelemetryNames.OutcomeMiss, activity);
                return ret;
            }
            catch (Exception e) when (FailOperation(CacheTelemetryNames.OperationTryRemove, start, e, activity))
            {
                throw;
            }
            catch (Exception e) when (!(e is ObjectDisposedException))
            {
                // Try contract: report failure (already recorded above) as false instead of throwing. Disposal still throws.
                val = default;
                return false;
            }
            finally
            {
                activity?.Dispose();
            }
        }

        private bool TryRemoveCore(T1 key, out T2 val)
        {
            ThrowIfDisposed();

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
                if (_Persistence != null)
                    InvokePersistenceAsync(CacheTelemetryNames.PersistenceDelete, p => p.DeleteAsync(key)).GetAwaiter().GetResult();
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
            ThrowIfDisposed();

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
            long start = OperationTimestamp();
            Activity activity = StartOperationActivity(CacheTelemetryNames.OperationPrepopulate);

            try
            {
                int ret = await PrepopulateCoreAsync(cancellationToken).ConfigureAwait(false);
                RecordPrepopulated(ret, activity);
                CompleteOperation(CacheTelemetryNames.OperationPrepopulate, start, CacheTelemetryNames.OutcomeSuccess, activity);
            }
            catch (Exception e) when (FailOperation(CacheTelemetryNames.OperationPrepopulate, start, e, activity))
            {
                throw;
            }
            finally
            {
                activity?.Dispose();
            }
        }

        private async Task<int> PrepopulateCoreAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (_Persistence == null)
                throw new InvalidOperationException("No persistence driver has been defined for the cache.");

            List<T1> keys = await InvokePersistenceAsync(CacheTelemetryNames.PersistenceEnumerate, p => p.EnumerateAsync(cancellationToken)).ConfigureAwait(false);

            int loaded = 0;

            if (keys != null && keys.Count > 0)
            {
                int loadCount = Math.Min(keys.Count, Capacity);

                for (int i = 0; i < loadCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    T1 key = keys[i];
                    T2 data = await InvokePersistenceAsync(CacheTelemetryNames.PersistenceGet, p => p.GetAsync(key, cancellationToken)).ConfigureAwait(false);
                    DataNode<T2> node = new DataNode<T2>(data);
                    long valueSize = MaxMemoryBytes > 0 ? EstimateSize(data) : 0;

                    bool added = false;

                    lock (_CacheLock)
                    {
                        if (_Cache.Count < Capacity)
                        {
                            // Race condition protection: check if key already exists
                            if (!_Cache.ContainsKey(key))
                            {
                                if (MaxMemoryBytes > 0 && CurrentMemoryBytes + valueSize > MaxMemoryBytes)
                                {
                                    break;
                                }

                                _Cache.Add(key, node);
                                if (MaxMemoryBytes > 0)
                                {
                                    CurrentMemoryBytes += valueSize;
                                }

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
                        loaded++;
                        _Events?.OnPrepopulated(this, new DataEventArgs<T1, T2>(key, node));
                    }
                }
            }

            return loaded;
        }

        #endregion

        #region Internal-Members

        /// <inheritdoc />
        internal override string TelemetryCacheType => CacheTelemetryNames.CacheTypeFifo;

        #endregion

        #region Internal-Methods

        /// <inheritdoc />
        internal override async Task ExpirationTask(CancellationToken token = default)
        {
            // The task is started from the constructor and inherits the caller's ambient span through the execution context.
            // Detach it so each sweep is its own trace root (linked back to the creating trace) instead of a child of an unrelated request.
            Activity.Current = null;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    await WaitForNextSweepAsync(token).ConfigureAwait(false);

                    long sweepStart = Stopwatch.GetTimestamp();
                    DateTime sweepStartUtc = DateTime.UtcNow;
                    Activity sweepActivity = null;
                    Exception sweepFailure = null;

                    try
                    {
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

                                RecordExpirations(expired.Count);
                            }
                        }

                        if (expired != null && expired.Count > 0)
                        {
                            sweepActivity = StartSweepActivity(sweepStartUtc, expired.Count);

                            // Each entry is processed independently: a failing persistence delete or Expired handler is
                            // recorded and the remaining entries are still processed. There is no caller to rethrow to.
                            foreach (KeyValuePair<T1, DataNode<T2>> entry in expired)
                            {
                                if (_Persistence != null)
                                {
                                    try
                                    {
                                        await InvokePersistenceAsync(CacheTelemetryNames.PersistenceDelete, p => p.DeleteAsync(entry.Key, token)).ConfigureAwait(false);
                                    }
                                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                                    {
                                        break;
                                    }
                                    catch (Exception e)
                                    {
                                        if (sweepFailure == null) sweepFailure = e;
                                    }
                                }

                                try
                                {
                                    _Events?.OnExpired(this, entry.Key);
                                }
                                catch (Exception e)
                                {
                                    if (sweepFailure == null) sweepFailure = e;
                                }
                            }
                        }

                        if (sweepFailure != null)
                            FailSweep(sweepStart, sweepFailure, sweepActivity);
                        else
                            CompleteSweep(sweepStart, sweepActivity);
                    }
                    catch (Exception e) when (FailSweep(sweepStart, e, sweepActivity))
                    {
                        throw;
                    }
                    finally
                    {
                        sweepActivity?.Dispose();
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception)
                {
                    // Already recorded by FailSweep. Keep the expiration task alive; the next sweep runs after the interval.
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

                UnregisterTelemetry();

                _Events?.OnDisposed(this, EventArgs.Empty);
                _Events = null;
                _Persistence = null;

                _TokenSource?.Dispose();
            }
        }

        #endregion
    }
}
