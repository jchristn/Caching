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
    public class LRUCacheBTree
    {
        /// <summary>
        /// Enable or disable console debugging.
        /// </summary>
        public bool Debug;

        private int Capacity;
        private int EvictCount;
        private readonly object CacheLock = new object();
        private BPlusTree<string, Tuple<object, DateTime, DateTime>> Cache = new BPlusTree<string, Tuple<object, DateTime, DateTime>>();
        // key, data, added, last_used

        /// <summary>
        /// Initialize the cache.
        /// </summary>
        /// <param name="capacity">Maximum number of entries.</param>
        /// <param name="evictCount">Number to evict when capacity is reached.</param>
        /// <param name="debug">Enable or disable console debugging.</param>
        public LRUCacheBTree(int capacity, int evictCount, bool debug)
        {
            Capacity = capacity;
            EvictCount = evictCount;
            Debug = debug;
            Cache = new BPlusTree<string, Tuple<object, DateTime, DateTime>>();
            Cache.EnableCount();

            if (EvictCount > Capacity)
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
            lock (CacheLock)
            {
                return Cache.Count;
            }
        }

        /// <summary>
        /// Retrieve the key of the oldest entry in the cache.
        /// </summary>
        /// <returns>String containing the key.</returns>
        public string Oldest()
        {
            if (Cache == null || Cache.Count < 1) return null;

            lock (CacheLock)
            {
                // key, Tuple<data, added, last_used>
                KeyValuePair<string, Tuple<object, DateTime, DateTime>> oldest = Cache.Where(x => x.Value.Item2 != null).OrderBy(x => x.Value.Item2).First();
                return oldest.Key;
            }
        }

        /// <summary>
        /// Retrieve the key of the newest entry in the cache.
        /// </summary>
        /// <returns>String containing the key.</returns>
        public string Newest()
        {
            if (Cache == null || Cache.Count < 1) return null;

            lock (CacheLock)
            {
                // key, Tuple<data, added, last_used>
                KeyValuePair<string, Tuple<object, DateTime, DateTime>> newest = Cache.Where(x => x.Value.Item2 != null).OrderBy(x => x.Value.Item2).Last();
                return newest.Key;
            }
        }

        /// <summary>
        /// Retrieve the key of the last used entry in the cache.
        /// </summary>
        /// <returns>String containing the key.</returns>
        public string LastUsed()
        {
            if (Cache == null || Cache.Count < 1) return null;

            lock (CacheLock)
            {
                // key, Tuple<data, added, last_used>
                KeyValuePair<string, Tuple<object, DateTime, DateTime>> newest = Cache.Where(x => x.Value.Item2 != null).OrderBy(x => x.Value.Item3).Last();
                return newest.Key;
            }
        }

        /// <summary>
        /// Retrieve the key of the first used entry in the cache.
        /// </summary>
        /// <returns>String containing the key.</returns>
        public string FirstUsed()
        {
            if (Cache == null || Cache.Count < 1) return null;

            lock (CacheLock)
            {
                // key, Tuple<data, added, last_used>
                KeyValuePair<string, Tuple<object, DateTime, DateTime>> oldest = Cache.Where(x => x.Value.Item2 != null).OrderBy(x => x.Value.Item3).First();
                return oldest.Key;
            }
        }

        /// <summary>
        /// Clear the cache.
        /// </summary>
        public void Clear()
        {
            lock (CacheLock)
            {
                Cache = new BPlusTree<string, Tuple<object, DateTime, DateTime>>();
                Cache.EnableCount();
                return;
            }
        }

        /// <summary>
        /// Retrieve a key's value from the cache.
        /// </summary>
        /// <param name="key">The key associated with the data you wish to retrieve.</param>
        /// <returns>The object data associated with the key.</returns>
        public object Get(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            lock (CacheLock)
            {
                // key, Tuple<data, added, last_used>
                List<KeyValuePair<string, Tuple<object, DateTime, DateTime>>> entries = new List<KeyValuePair<string, Tuple<object, DateTime, DateTime>>>();
                
                if (Cache.Count > 0) entries = Cache.Where(x => x.Key == key).ToList();
                else entries = null;

                if (entries == null) return null;
                else
                {
                    if (entries.Count > 0)
                    {
                        foreach (KeyValuePair<string, Tuple<object, DateTime, DateTime>> curr in entries)
                        {
                            Cache.Remove(curr.Key);
                            Tuple<object, DateTime, DateTime> val = new Tuple<object, DateTime, DateTime>(curr.Value.Item1, curr.Value.Item2, DateTime.Now);
                            Cache.Add(curr.Key, val);
                            return curr.Value.Item1;
                        }
                    }

                    return null;
                }
            }
        }

        /// <summary>
        /// Add or replace a key's value in the cache.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="val">The value associated with the key.</param>
        /// <returns>Boolean indicating success.</returns>
        public bool AddReplace(string key, object val)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            lock (CacheLock)
            {
                if (Cache.Count >= Capacity)
                {
                    int evictedCount = 0;
                    while (evictedCount < EvictCount)
                    {
                        KeyValuePair<string, Tuple<object, DateTime, DateTime>> oldest = Cache.Where(x => x.Value.Item2 != null).OrderBy(x => x.Value.Item3).First();
                        Cache.Remove(oldest.Key);
                        evictedCount++;
                    }
                }

                List<KeyValuePair<string, Tuple<object, DateTime, DateTime>>> dupes = new List<KeyValuePair<string, Tuple<object, DateTime, DateTime>>>();
                if (Cache.Count > 0) dupes = Cache.Where(x => x.Key.ToLower() == key).ToList();
                else dupes = null;

                if (dupes == null)
                {
                    #region New-Entry
                    
                    Tuple<object, DateTime, DateTime> value = new Tuple<object, DateTime, DateTime>(val, DateTime.Now, DateTime.Now);
                    Cache.Add(key, value);
                    return true;

                    #endregion
                }
                else if (dupes.Count > 0)
                {
                    #region Duplicate-Entries-Exist
                    
                    foreach (KeyValuePair<string, Tuple<object, DateTime, DateTime>> curr in dupes)
                    {
                        Cache.Remove(curr.Key);
                    }
                    
                    Tuple<object, DateTime, DateTime> value = new Tuple<object, DateTime, DateTime>(val, DateTime.Now, DateTime.Now);
                    Cache.Add(key, value);
                    return true;

                    #endregion
                }
                else
                {
                    #region New-Entry
                    
                    Tuple<object, DateTime, DateTime> value = new Tuple<object, DateTime, DateTime>(val, DateTime.Now, DateTime.Now);
                    Cache.Add(key, value);
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

            lock (CacheLock)
            {
                List<KeyValuePair<string, Tuple<object, DateTime, DateTime>>> dupes = new List<KeyValuePair<string, Tuple<object, DateTime, DateTime>>>();

                if (Cache.Count > 0) dupes = Cache.Where(x => x.Key.ToLower() == key).ToList();
                else dupes = null;

                if (dupes == null) return true;
                else if (dupes.Count < 1) return true;
                else
                {
                    foreach (KeyValuePair<string, Tuple<object, DateTime, DateTime>> curr in dupes)
                    {
                        Cache.Remove(curr.Key);
                    }
                    
                    return true;
                }
            }
        }
    }
}
