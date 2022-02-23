using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Caching
{
    /// <summary>
    /// LRU cache that internally uses tuples.  T1 is the type of the key, and T2 is the type of the value.
    /// </summary>
    public class LRUCache<T1, T2> : IDisposable
    {
        #region Public-Members

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

        #endregion

        #region Private-Members

        private int _Capacity = 0;
        private int _EvictCount = 0;
        private readonly object _CacheLock = new object();
        private Dictionary<T1, DataNode<T2>> _Cache = new Dictionary<T1, DataNode<T2>>();
        private PersistenceDriver _Persistence = null;
        private CacheEvents<T1, T2> _Events = new CacheEvents<T1, T2>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initialize the cache.
        /// </summary>
        /// <param name="capacity">Maximum number of entries.</param>
        /// <param name="evictCount">Number to evict when capacity is reached.</param>
        /// <param name="persistence">Persistence driver.</param>
        /// <param name="prepopulate">If using persistence, prepopulate from existing items.</param>
        public LRUCache(int capacity, int evictCount, PersistenceDriver persistence = null, bool prepopulate = true)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (evictCount < 1) throw new ArgumentOutOfRangeException(nameof(evictCount));
            if (evictCount > capacity) throw new ArgumentOutOfRangeException(nameof(evictCount));

            if (persistence != null)
            {
                if (typeof(T1) != typeof(string))
                    throw new InvalidOperationException("Persistence can only be used when the cache key is of type 'string'.");
            }

            _Capacity = capacity;
            _EvictCount = evictCount;
            _Cache = new Dictionary<T1, DataNode<T2>>();
            _Persistence = persistence;

            if (persistence != null && prepopulate)
            {
                Prepopulate();
            }
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
        public int Count()
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
        public T1 Oldest()
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
        public T1 Newest()
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
        public Dictionary<T1, T2> All()
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
        /// Retrieve the key of the last used entry in the cache.
        /// </summary>
        /// <returns>String containing the key.</returns>
        public T1 LastUsed()
        {
            if (_Cache == null || _Cache.Count < 1) throw new KeyNotFoundException();

            lock (_CacheLock)
            {
                KeyValuePair<T1, DataNode<T2>> lastUsed = _Cache.OrderBy(x => x.Value.LastUsed).Last();
                return lastUsed.Key;
            }
        }

        /// <summary>
        /// Retrieve the key of the first used entry in the cache.
        /// </summary>
        /// <returns>String containing the key.</returns>
        public T1 FirstUsed()
        {
            if (_Cache == null || _Cache.Count < 1) throw new KeyNotFoundException();

            lock (_CacheLock)
            {
                KeyValuePair<T1, DataNode<T2>> firstUsed = _Cache.OrderBy(x => x.Value.LastUsed).First();
                return firstUsed.Key;
            }
        }

        /// <summary>
        /// Clear the cache.
        /// </summary>
        public void Clear()
        {
            lock (_CacheLock)
            {
                if (_Persistence != null)
                {
                    foreach (KeyValuePair<T1, DataNode<T2>> kvp in _Cache)
                    {
                        _Persistence.Delete(kvp.Key.ToString());
                    }
                }

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
        public T2 Get(T1 key)
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
        public bool TryGet(T1 key, out T2 val)
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
                    val = default(T2);
                    return false;
                }
            }
        }

        /// <summary>
        /// See if a key exists in the cache.
        /// </summary>
        /// <param name="key">The key of the cached items.</param>
        /// <returns>True if cached.</returns>
        public bool Contains(T1 key)
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
        /// <returns>Boolean indicating success.</returns>
        public void AddReplace(T1 key, T2 val)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            lock (_CacheLock)
            {
                bool replaced = false;
                DataNode<T2> previous = null;

                if (_Cache.ContainsKey(key))
                {
                    if (_Persistence != null && _Persistence.Exists(key.ToString()))
                    {
                        _Persistence.Delete(key.ToString());
                    }

                    previous = _Cache[key];
                    _Cache.Remove(key);

                    replaced = true;
                }

                if (_Cache.Count >= _Capacity)
                {
                    Dictionary<T1, DataNode<T2>> updated = _Cache.OrderBy(x => x.Value.LastUsed).Skip(_EvictCount).ToDictionary(x => x.Key, x => x.Value);
                    Dictionary<T1, DataNode<T2>> removed = _Cache.Except(updated).ToDictionary(x => x.Key, x => x.Value);

                    if (_Persistence != null && removed != null && removed.Count > 0)
                    {
                        foreach (KeyValuePair<T1, DataNode<T2>> kvp in removed)
                        {
                            _Persistence.Delete(kvp.Key.ToString());
                        }
                    }

                    _Cache = updated;

                    if (removed != null && removed.Count > 0)
                    {
                        List<T1> evictedKeys = new List<T1>(removed.Keys);
                        _Events?.Evicted?.Invoke(this, evictedKeys);
                    }
                }

                DataNode<T2> curr = new DataNode<T2>(val);
                _Cache.Add(key, curr);

                if (_Persistence != null)
                {
                    _Persistence.Write(key.ToString(), _Persistence.ToBytes(val));
                }

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
        /// <returns>True if successful.</returns>
        public bool TryAddReplace(T1 key, T2 val)
        {
            try
            {
                AddReplace(key, val);
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
        public void Remove(T1 key)
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

                if (_Persistence != null)
                {
                    _Persistence.Delete(key.ToString());
                }

                _Events?.Removed?.Invoke(this, new DataEventArgs<T1, T2>(key, val));
                return;
            }
        }

        /// <summary>
        /// Retrieve all keys in the cache.
        /// </summary>
        /// <returns>List of string.</returns>
        public List<T1> GetKeys()
        {
            lock (_CacheLock)
            {
                List<T1> keys = new List<T1>(_Cache.Keys);
                return keys;
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
                lock (_CacheLock)
                {
                    _Cache = null;
                }

                _Capacity = 0;
                _EvictCount = 0;

                _Events?.Disposed?.Invoke(this, EventArgs.Empty);
                _Events = null;
                _Persistence = null;
            }
        }

        private void Prepopulate()
        {
            List<string> files = _Persistence.Enumerate();

            if (files != null && files.Count > 0)
            {
                foreach (string file in files)
                {
                    byte[] data = _Persistence.Get(file);

                    T1 key = (T1)Convert.ChangeType(file, typeof(T1));
                    DataNode<T2> node = new DataNode<T2>(_Persistence.FromBytes<T2>(data));

                    _Cache.Add(key, node);
                    _Events?.Prepopulated?.Invoke(this, new DataEventArgs<T1, T2>(key, node));
                }
            }
        }

        #endregion
    }
}
