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
    /// FIFO cache that internally uses tuples.
    /// </summary>
    public class FIFOCache<T>
    {
        /// <summary>
        /// Enable or disable console debugging.
        /// </summary>
        public bool Debug;

        private int _Capacity;
        private int _EvictCount;
        private readonly object _CacheLock = new object();
        private Dictionary<string, DataNode<T>> _Cache;

        /// <summary>
        /// Initialize the cache.
        /// </summary>
        /// <param name="capacity">Maximum number of entries.</param>
        /// <param name="evictCount">Number to evict when capacity is reached.</param>
        /// <param name="debug">Enable or disable console debugging.</param>
        public FIFOCache(int capacity, int evictCount, bool debug)
        {
            _Capacity = capacity;
            _EvictCount = evictCount;
            _Cache = new Dictionary<string, DataNode<T>>();

            Debug = debug;

            if (_EvictCount > _Capacity)
            {
                throw new ArgumentException("Evict count must be less than or equal to capacity.");
            }
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
        public string Oldest()
        {
            if (_Cache == null || _Cache.Count < 1) return null;

            lock (_CacheLock)
            {
                KeyValuePair<string, DataNode<T>> oldest = _Cache.Where(x => x.Value.Added != null).OrderBy(x => x.Value.Added).First();
                return oldest.Key;
            }
        }

        /// <summary>
        /// Retrieve the key of the newest entry in the cache.
        /// </summary>
        /// <returns>String containing the key.</returns>
        public string Newest()
        {
            if (_Cache == null || _Cache.Count < 1) return null;

            lock (_CacheLock)
            {
                KeyValuePair<string, DataNode<T>> newest = _Cache.Where(x => x.Value.Added != null).OrderBy(x => x.Value.Added).Last();
                return newest.Key;
            }
        }

        /// <summary>
        /// Clear the cache.
        /// </summary>
        public void Clear()
        {
            lock (_CacheLock)
            {
                _Cache = new Dictionary<string, DataNode<T>>();
                return;
            }
        }

        /// <summary>
        /// Retrieve a key's value from the cache.
        /// </summary>
        /// <param name="key">The key associated with the data you wish to retrieve.</param>
        /// <returns>The object data associated with the key.</returns>
        public T Get(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            lock (_CacheLock)
            {
                if (_Cache.ContainsKey(key))
                {
                    KeyValuePair<string, DataNode<T>> curr = _Cache.Where(x => x.Key.Equals(key)).First();

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
        public bool TryGet(string key, out T val)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            lock (_CacheLock)
            {
                if (_Cache.ContainsKey(key))
                {
                    KeyValuePair<string, DataNode<T>> curr = _Cache.Where(x => x.Key.Equals(key)).First();

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
                    val = default(T);
                    return false;
                }
            }
        }

        /// <summary>
        /// Add or replace a key's value in the cache.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="val">The value associated with the key.</param> 
        public void AddReplace(string key, T val)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            lock (_CacheLock)
            {
                if (_Cache.ContainsKey(key))
                {
                    _Cache.Remove(key);
                }

                if (_Cache.Count >= _Capacity)
                {
                    _Cache = _Cache.OrderBy(x => x.Value.Added).Skip(_EvictCount).ToDictionary(x => x.Key, x => x.Value);
                }
                 
                DataNode<T> curr = new DataNode<T>(val);
                _Cache.Add(key, curr);
                return;
            }
        }

        /// <summary>
        /// Remove a key from the cache.
        /// </summary>
        /// <param name="key">The key.</param> 
        public void Remove(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            lock (_CacheLock)
            {
                if (_Cache.ContainsKey(key))
                {
                    _Cache.Remove(key);
                }

                return;
            }
        }

        /// <summary>
        /// Retrieve all keys in the cache.
        /// </summary>
        /// <returns>List of string.</returns>
        public List<string> GetKeys()
        {
            lock (_CacheLock)
            {
                List<string> keys = new List<string>(_Cache.Keys);
                return keys;
            }
        }
    }
}
