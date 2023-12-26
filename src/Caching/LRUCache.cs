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
    public class LRUCache<T1, T2> : ICache<T1, T2>, IDisposable
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

            Task.Run(() => ExpirationTask(_Token));
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

            Task.Run(() => ExpirationTask(_Token));
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
        /// Retrieve the key of the oldest entry in the cache.
        /// </summary>
        /// <returns>String containing the key.</returns>
        public override T1 Oldest()
        {
            if (_Cache == null || _Cache.Count < 1) throw new KeyNotFoundException();

            lock (_CacheLock)
            {
                KeyValuePair<T1, DataNode<T2>> oldest = _Cache.OrderBy(x => x.Value.Added).First();
                return oldest.Key;
            }
        }

        /// <summary>
        /// Retrieve the key of the newest entry in the cache.
        /// </summary>
        /// <returns>String containing the key.</returns>
        public override T1 Newest()
        {
            if (_Cache == null || _Cache.Count < 1) throw new KeyNotFoundException();

            lock (_CacheLock)
            {
                KeyValuePair<T1, DataNode<T2>> newest = _Cache.OrderBy(x => x.Value.Added).Last();
                return newest.Key;
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
            lock (_CacheLock)
            {
                _Persistence?.Clear();
                _Cache = new Dictionary<T1, DataNode<T2>>();
                _Events?.Cleared?.Invoke(this, EventArgs.Empty);
                return;
            }
        }

        /// <summary>
        /// Retrieve a key's value from the cache.
        /// </summary>
        /// <param name="key">The key associated with the data you wish to retrieve.</param>
        /// <returns>The object data associated with the key.</returns>
        public override T2 Get(T1 key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            lock (_CacheLock)
            {
                if (_Cache.ContainsKey(key))
                {
                    KeyValuePair<T1, DataNode<T2>> curr = _Cache.Where(x => x.Key.Equals(key)).First();

                    // update LastUsed
                    _Cache.Remove(key);
                    curr.Value.LastUsed = DateTime.Now;
                    _Cache.Add(key, curr.Value);

                    // return data
                    return curr.Value.Data;
                }
                else
                {
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
            if (key == null) throw new ArgumentNullException(nameof(key));

            lock (_CacheLock)
            {
                if (_Cache.ContainsKey(key))
                {
                    KeyValuePair<T1, DataNode<T2>> curr = _Cache.Where(x => x.Key.Equals(key)).First();

                    // update LastUsed
                    _Cache.Remove(key);
                    curr.Value.LastUsed = DateTime.Now;
                    _Cache.Add(key, curr.Value);

                    // return data
                    val = curr.Value.Data;
                    return true;
                }
                else
                {
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
            if (key == null) throw new ArgumentNullException(nameof(key));

            lock (_CacheLock)
            {
                if (_Cache.ContainsKey(key))
                {
                    return true;
                }
            }

            return false;
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
            if (key == null) throw new ArgumentNullException(nameof(key));

            if (expiration != null)
            {
                expiration = expiration.Value.ToUniversalTime();
                if (DateTime.UtcNow > expiration.Value)
                    throw new ArgumentException("The specified expiration timestamp is in the past.");
            }

            lock (_CacheLock)
            {
                bool replaced = false;
                DataNode<T2> previous = null;

                if (_Cache.ContainsKey(key))
                {
                    if (_Persistence != null && _Persistence.Exists(key))
                    {
                        _Persistence.Delete(key);
                    }

                    previous = _Cache[key];
                    _Cache.Remove(key);

                    replaced = true;
                }

                if (_Cache.Count >= Capacity)
                {
                    Dictionary<T1, DataNode<T2>> updated = _Cache.OrderBy(x => x.Value.LastUsed).Skip(EvictCount).ToDictionary(x => x.Key, x => x.Value);
                    Dictionary<T1, DataNode<T2>> removed = _Cache.Except(updated).ToDictionary(x => x.Key, x => x.Value);

                    if (_Persistence != null && removed != null && removed.Count > 0)
                    {
                        foreach (KeyValuePair<T1, DataNode<T2>> kvp in removed)
                        {
                            _Persistence.Delete(kvp.Key);
                        }
                    }

                    _Cache = updated;

                    if (removed != null && removed.Count > 0)
                    {
                        List<T1> evictedKeys = new List<T1>(removed.Keys);
                        _Events?.Evicted?.Invoke(this, evictedKeys);
                    }
                }

                DataNode<T2> curr = new DataNode<T2>(val, expiration);
                _Cache.Add(key, curr);

                _Persistence?.Write(key, val);

                if (replaced) _Events?.Replaced?.Invoke(this, new DataEventArgs<T1, T2>(key, previous));
                _Events?.Added?.Invoke(this, new DataEventArgs<T1, T2>(key, curr));

                return;
            }
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
        /// Remove a key from the cache.
        /// </summary>
        /// <param name="key">The key.</param> 
        public override void Remove(T1 key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            lock (_CacheLock)
            {
                DataNode<T2> val = null;

                if (_Cache.ContainsKey(key))
                {
                    val = _Cache[key];
                    _Cache.Remove(key);
                }

                _Persistence?.Delete(key);

                _Events?.Removed?.Invoke(this, new DataEventArgs<T1, T2>(key, val));
                return;
            }
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
            if (_Persistence == null) throw new InvalidOperationException("No persistence driver has been defined for the cache.");

            List<T1> objects = _Persistence.Enumerate();

            if (objects != null && objects.Count > 0)
            {
                foreach (T1 obj in objects)
                {
                    T2 data = _Persistence.Get(obj);

                    DataNode<T2> node = new DataNode<T2>(data);

                    _Cache.Add(obj, node);
                    _Events?.Prepopulated?.Invoke(this, new DataEventArgs<T1, T2>(obj, node));
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
                await Task.Delay(_ExpirationIntervalMs);

                lock (_CacheLock)
                {
                    Dictionary<T1, DataNode<T2>> expired = _Cache.Where(
                        c => c.Value.Expiration != null && c.Value.Expiration.Value < DateTime.UtcNow)
                        .ToDictionary(c => c.Key, c => c.Value);

                    if (expired != null && expired.Count > 0)
                    {
                        foreach (KeyValuePair<T1, DataNode<T2>> entry in expired)
                        {
                            _Cache.Remove(entry.Key);
                            _Events.Expired(this, entry.Key);
                        }
                    }
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
            if (disposing)
            {
                CancellationTokenSource ctsLinked = CancellationTokenSource.CreateLinkedTokenSource(_Token);
                ctsLinked.Cancel();

                lock (_CacheLock)
                {
                    _Cache = null;
                }

                Capacity = 0;
                EvictCount = 0;

                _Events?.Disposed?.Invoke(this, EventArgs.Empty);
                _Events = null;
                _Persistence = null;
            }
        }

        #endregion
    }
}
