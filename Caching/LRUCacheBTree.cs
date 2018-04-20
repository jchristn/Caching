using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CSharpTest.Net.Collections;
using CSharpTest.Net.Serialization;

namespace Caching
{   
    /// <summary>
    /// LRU cache that internally uses a BTree (refer to CSharpTest.Net).
    /// </summary>
    public class LRUCacheBTree<T>
    {
        /// <summary>
        /// Enable or disable console debugging.
        /// </summary>
        public bool Debug;

        private int _Capacity;
        private int _EvictCount;
        private readonly object _CacheLock = new object();
        private BPlusTree<string, Tuple<T, DateTime, DateTime>> _Cache = new BPlusTree<string, Tuple<T, DateTime, DateTime>>();
        // key, data, added, last_used

        /// <summary>
        /// Initialize the cache.
        /// </summary>
        /// <param name="capacity">Maximum number of entries.</param>
        /// <param name="evictCount">Number to evict when capacity is reached.</param>
        /// <param name="debug">Enable or disable console debugging.</param>
        public LRUCacheBTree(int capacity, int evictCount, bool debug)
        {
            _Capacity = capacity;
            _EvictCount = evictCount;
            Debug = debug;
            _Cache = new BPlusTree<string, Tuple<T, DateTime, DateTime>>();
            _Cache.EnableCount();

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
                // key, Tuple<data, added, last_used>
                KeyValuePair<string, Tuple<T, DateTime, DateTime>> oldest = _Cache.Where(x => x.Value.Item2 != null).OrderBy(x => x.Value.Item2).First();
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
                // key, Tuple<data, added, last_used>
                KeyValuePair<string, Tuple<T, DateTime, DateTime>> newest = _Cache.Where(x => x.Value.Item2 != null).OrderBy(x => x.Value.Item2).Last();
                return newest.Key;
            }
        }

        /// <summary>
        /// Retrieve the key of the last used entry in the cache.
        /// </summary>
        /// <returns>String containing the key.</returns>
        public string LastUsed()
        {
            if (_Cache == null || _Cache.Count < 1) return null;

            lock (_CacheLock)
            {
                // key, Tuple<data, added, last_used>
                KeyValuePair<string, Tuple<T, DateTime, DateTime>> newest = _Cache.Where(x => x.Value.Item2 != null).OrderBy(x => x.Value.Item3).Last();
                return newest.Key;
            }
        }

        /// <summary>
        /// Retrieve the key of the first used entry in the cache.
        /// </summary>
        /// <returns>String containing the key.</returns>
        public string FirstUsed()
        {
            if (_Cache == null || _Cache.Count < 1) return null;

            lock (_CacheLock)
            {
                // key, Tuple<data, added, last_used>
                KeyValuePair<string, Tuple<T, DateTime, DateTime>> oldest = _Cache.Where(x => x.Value.Item2 != null).OrderBy(x => x.Value.Item3).First();
                return oldest.Key;
            }
        }

        /// <summary>
        /// Clear the cache.
        /// </summary>
        public void Clear()
        {
            lock (_CacheLock)
            {
                _Cache = new BPlusTree<string, Tuple<T, DateTime, DateTime>>();
                _Cache.EnableCount();
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
                // key, Tuple<data, added, last_used>
                List<KeyValuePair<string, Tuple<T, DateTime, DateTime>>> entries = new List<KeyValuePair<string, Tuple<T, DateTime, DateTime>>>();
                
                if (_Cache.Count > 0) entries = _Cache.Where(x => x.Key == key).ToList();
                else entries = null;

                if (entries == null) throw new KeyNotFoundException();
                else
                {
                    if (entries.Count > 0)
                    {
                        foreach (KeyValuePair<string, Tuple<T, DateTime, DateTime>> curr in entries)
                        {
                            _Cache.Remove(curr.Key);
                            Tuple<T, DateTime, DateTime> val = new Tuple<T, DateTime, DateTime>(curr.Value.Item1, curr.Value.Item2, DateTime.Now);
                            _Cache.Add(curr.Key, val);
                            return curr.Value.Item1;
                        }
                    }

                    throw new KeyNotFoundException();
                }
            }
        }

        /// <summary>
        /// Add or replace a key's value in the cache.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="val">The value associated with the key.</param>
        /// <returns>Boolean indicating success.</returns>
        public bool AddReplace(string key, T val)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            lock (_CacheLock)
            {
                if (_Cache.Count >= _Capacity)
                {
                    int evictedCount = 0;
                    while (evictedCount < _EvictCount)
                    {
                        KeyValuePair<string, Tuple<T, DateTime, DateTime>> oldest = _Cache.Where(x => x.Value.Item2 != null).OrderBy(x => x.Value.Item3).First();
                        _Cache.Remove(oldest.Key);
                        evictedCount++;
                    }
                }

                List<KeyValuePair<string, Tuple<T, DateTime, DateTime>>> dupes = new List<KeyValuePair<string, Tuple<T, DateTime, DateTime>>>();
                if (_Cache.Count > 0) dupes = _Cache.Where(x => x.Key.ToLower() == key).ToList();
                else dupes = null;

                if (dupes == null)
                {
                    #region New-Entry
                    
                    Tuple<T, DateTime, DateTime> value = new Tuple<T, DateTime, DateTime>(val, DateTime.Now, DateTime.Now);
                    _Cache.Add(key, value);
                    return true;

                    #endregion
                }
                else if (dupes.Count > 0)
                {
                    #region Duplicate-Entries-Exist
                    
                    foreach (KeyValuePair<string, Tuple<T, DateTime, DateTime>> curr in dupes)
                    {
                        _Cache.Remove(curr.Key);
                    }
                    
                    Tuple<T, DateTime, DateTime> value = new Tuple<T, DateTime, DateTime>(val, DateTime.Now, DateTime.Now);
                    _Cache.Add(key, value);
                    return true;

                    #endregion
                }
                else
                {
                    #region New-Entry
                    
                    Tuple<T, DateTime, DateTime> value = new Tuple<T, DateTime, DateTime>(val, DateTime.Now, DateTime.Now);
                    _Cache.Add(key, value);
                    return true;

                    #endregion
                }
            }
        }

        /// <summary>
        /// Remove a key from the cache.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>Boolean indicating success.</returns>
        public bool Remove(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            lock (_CacheLock)
            {
                List<KeyValuePair<string, Tuple<T, DateTime, DateTime>>> dupes = new List<KeyValuePair<string, Tuple<T, DateTime, DateTime>>>();

                if (_Cache.Count > 0) dupes = _Cache.Where(x => x.Key.ToLower() == key).ToList();
                else dupes = null;

                if (dupes == null) return true;
                else if (dupes.Count < 1) return true;
                else
                {
                    foreach (KeyValuePair<string, Tuple<T, DateTime, DateTime>> curr in dupes)
                    {
                        _Cache.Remove(curr.Key);
                    }
                    
                    return true;
                }
            }
        }
    }
}
