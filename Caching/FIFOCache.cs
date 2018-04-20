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
        private List<Tuple<string, T, DateTime>> _Lock { get; set; }

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
            _Lock = new List<Tuple<string, T, DateTime>>();

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
                return _Lock.Count;
            }
        }

        /// <summary>
        /// Retrieve the key of the oldest entry in the cache.
        /// </summary>
        /// <returns>String containing the key.</returns>
        public string Oldest()
        {
            lock (_CacheLock)
            { 
                Tuple<string, T, DateTime> oldest = _Lock.Where(x => x.Item3 != null).OrderBy(x => x.Item3).First();
                return oldest.Item1;
            }
        }

        /// <summary>
        /// Retrieve the key of the newest entry in the cache.
        /// </summary>
        /// <returns>String containing the key.</returns>
        public string Newest()
        {
            lock (_CacheLock)
            { 
                Tuple<string, T, DateTime> newest = _Lock.Where(x => x.Item3 != null).OrderBy(x => x.Item3).Last();
                return newest.Item1;
            }
        }

        /// <summary>
        /// Clear the cache.
        /// </summary>
        public void Clear()
        {
            lock (_CacheLock)
            { 
                _Lock = new List<Tuple<string, T, DateTime>>();
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
                List<Tuple<string, T, DateTime>> entries = new List<Tuple<string, T, DateTime>>();

                if (_Lock.Count > 0) entries = _Lock.Where(x => x.Item1 == key).ToList();
                else entries = null;

                if (entries == null) throw new KeyNotFoundException();
                else
                {
                    if (entries.Count > 0)
                    {
                        foreach (Tuple<string, T, DateTime> curr in entries)
                        {
                            return curr.Item2;
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
                if (_Lock.Count >= _Capacity)
                {
                    _Lock = _Lock.OrderBy(x => x.Item3).Skip(_EvictCount).ToList();
                }

                List<Tuple<string, T, DateTime>> dupes = new List<Tuple<string, T, DateTime>>();

                if (_Lock.Count > 0) dupes = _Lock.Where(x => x.Item1.ToLower() == key).ToList();
                else dupes = null;

                if (dupes == null)
                {
                    #region New-Entry

                    _Lock.Add(new Tuple<string, T, DateTime>(key, val, DateTime.Now));
                    return true;

                    #endregion
                }
                else if (dupes.Count > 0)
                {
                    #region Duplicate-Entries-Exist

                    foreach (Tuple<string, T, DateTime> curr in dupes)
                    {
                        _Lock.Remove(curr);
                    }

                    _Lock.Add(new Tuple<string, T, DateTime>(key, val, DateTime.Now));
                    return true;

                    #endregion
                }
                else
                {
                    #region New-Entry

                    _Lock.Add(new Tuple<string, T, DateTime>(key, val, DateTime.Now));
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
                List<Tuple<string, T, DateTime>> dupes = new List<Tuple<string, T, DateTime>>();

                if (_Lock.Count > 0) dupes = _Lock.Where(x => x.Item1.ToLower() == key).ToList();
                else dupes = null;

                if (dupes == null) return true;
                else if (dupes.Count < 1) return true;
                else
                {
                    foreach (Tuple<string, T, DateTime> curr in dupes)
                    {
                        _Lock.Remove(curr);
                    }

                    return true;
                }
            }
        }
    }
}
