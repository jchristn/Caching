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
    /// LRU cache that internally uses tuples.
    /// </summary>
    public class LRUCache
    {
        /// <summary>
        /// Enable or disable console debugging.
        /// </summary>
        public bool Debug;

        private int Capacity;
        private int EvictCount;
        private static readonly Mutex Mtx = new Mutex();
        private List<Tuple<string, object, DateTime, DateTime>> Cache { get; set; }
        // key, data, added, last_used

        /// <summary>
        /// Initialize the cache.
        /// </summary>
        /// <param name="capacity">Maximum number of entries.</param>
        /// <param name="evictCount">Number to evict when capacity is reached.</param>
        /// <param name="debug">Enable or disable console debugging.</param>
        public LRUCache(int capacity, int evictCount, bool debug)
        {
            Capacity = capacity;
            EvictCount = evictCount;
            Debug = debug;
            Cache = new List<Tuple<string, object, DateTime, DateTime>>();

            if (EvictCount > Capacity)
            {
                throw new ArgumentException("Evict count must be less than or equal to capacity.");
            }

            Log("LRUCache initialized successfully with capacity " + capacity + " evictCount " + evictCount);
        }

        /// <summary>
        /// Retrieve the current number of entries in the cache.
        /// </summary>
        /// <returns>An integer containing the number of entries.</returns>
        public int Count()
        {
            DateTime startTime = DateTime.Now;
            Mtx.WaitOne();

            try
            {
                Log("LRUCache count " + Cache.Count);
                return Cache.Count;
            }
            catch (Exception e)
            {
                LogException(e);
                throw e;
            }
            finally
            {
                Mtx.ReleaseMutex();
            }
        }

        /// <summary>
        /// Retrieve the key of the oldest entry in the cache.
        /// </summary>
        /// <returns>String containing the key.</returns>
        public string Oldest()
        {
            if (Cache == null) return null;
            if (Cache.Count < 1) return null;

            DateTime startTime = DateTime.Now;
            Mtx.WaitOne();

            try
            {
                Tuple<string, object, DateTime, DateTime> oldest = Cache.Where(x => x.Item3 != null).OrderBy(x => x.Item3).First();
                Log("LRUCache oldest key " + oldest.Item1 + ": " + oldest.Item3.ToString("MM/dd/yyyy hh:mm:ss"));
                return oldest.Item1;
            }
            catch (Exception e)
            {
                LogException(e);
                throw e;
            }
            finally
            {
                Mtx.ReleaseMutex();
            }
        }

        /// <summary>
        /// Retrieve the key of the newest entry in the cache.
        /// </summary>
        /// <returns>String containing the key.</returns>
        public string Newest()
        {
            if (Cache == null) return null;
            if (Cache.Count < 1) return null;

            DateTime startTime = DateTime.Now;
            Mtx.WaitOne();

            try
            {
                Tuple<string, object, DateTime, DateTime> newest = Cache.Where(x => x.Item3 != null).OrderBy(x => x.Item3).Last();
                Log("LRUCache newest key " + newest.Item1 + ": " + newest.Item3.ToString("MM/dd/yyyy hh:mm:ss"));
                return newest.Item1;
            }
            catch (Exception e)
            {
                LogException(e);
                throw e;
            }
            finally
            {
                Mtx.ReleaseMutex();
            }
        }

        /// <summary>
        /// Retrieve the key of the last used entry in the cache.
        /// </summary>
        /// <returns>String containing the key.</returns>
        public string LastUsed()
        {
            if (Cache == null) return null;
            if (Cache.Count < 1) return null;

            DateTime startTime = DateTime.Now;
            Mtx.WaitOne();

            try
            {
                Tuple<string, object, DateTime, DateTime> newest = Cache.Where(x => x.Item4 != null).OrderBy(x => x.Item4).Last();
                Log("LRUCache last used key " + newest.Item1 + ": " + newest.Item4.ToString("MM/dd/yyyy hh:mm:ss"));
                return newest.Item1;
            }
            catch (Exception e)
            {
                LogException(e);
                throw e;
            }
            finally
            {
                Mtx.ReleaseMutex();
            }
        }

        /// <summary>
        /// Retrieve the key of the first used entry in the cache.
        /// </summary>
        /// <returns>String containing the key.</returns>
        public string FirstUsed()
        {
            if (Cache == null) return null;
            if (Cache.Count < 1) return null;

            DateTime startTime = DateTime.Now;
            Mtx.WaitOne();

            try
            {
                Tuple<string, object, DateTime, DateTime> oldest = Cache.Where(x => x.Item4 != null).OrderBy(x => x.Item4).First();
                Log("LRUCache first used key " + oldest.Item1 + ": " + oldest.Item4.ToString("MM/dd/yyyy hh:mm:ss"));
                return oldest.Item1;
            }
            catch (Exception e)
            {
                LogException(e);
                throw e;
            }
            finally
            {
                Mtx.ReleaseMutex();
            }
        }

        /// <summary>
        /// Clear the cache.
        /// </summary>
        public void Clear()
        {
            DateTime startTime = DateTime.Now;
            Mtx.WaitOne();

            try
            {
                Cache = new List<Tuple<string, object, DateTime, DateTime>>();
                return;
            }
            catch (Exception e)
            {
                LogException(e);
                throw e;
            }
            finally
            {
                Mtx.ReleaseMutex();
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

            DateTime startTime = DateTime.Now;
            Mtx.WaitOne();

            try
            {
                List<Tuple<string, object, DateTime, DateTime>> entries = new List<Tuple<string, object, DateTime, DateTime>>();

                if (Cache.Count > 0) entries = Cache.Where(x => x.Item1 == key).ToList();
                else entries = null;

                if (entries == null) return null;
                else
                {
                    if (entries.Count > 0)
                    {
                        foreach (Tuple<string, object, DateTime, DateTime> curr in entries)
                        {
                            Cache.Remove(curr);
                            Tuple<string, object, DateTime, DateTime> updated = new Tuple<string, object, DateTime, DateTime>(curr.Item1, curr.Item2, curr.Item3, DateTime.Now);
                            Cache.Add(updated);
                            return curr.Item2;
                        }
                    }

                    return null;
                }
            }
            catch (Exception e)
            {
                LogException(e);
                throw e;
            }
            finally
            {
                Mtx.ReleaseMutex();
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

            DateTime startTime = DateTime.Now;
            Mtx.WaitOne();

            try
            {
                if (Cache.Count >= Capacity)
                { 
                    Log("LRUCache cache full, evicting " + EvictCount);
                    Cache = Cache.OrderBy(x => x.Item4).Skip(EvictCount).ToList();
                }

                List<Tuple<string, object, DateTime, DateTime>> dupes = new List<Tuple<string, object, DateTime, DateTime>>();

                if (Cache.Count > 0) dupes = Cache.Where(x => x.Item1.ToLower() == key).ToList();
                else dupes = null;

                if (dupes == null)
                {
                    #region New-Entry
                    
                    Cache.Add(new Tuple<string, object, DateTime, DateTime>(key, val, DateTime.Now, DateTime.Now));
                    return true;

                    #endregion
                }
                else if (dupes.Count > 0)
                {
                    #region Duplicate-Entries-Exist
                    
                    foreach (Tuple<string, object, DateTime, DateTime> curr in dupes)
                    {
                        Cache.Remove(curr);
                    }
                    
                    Cache.Add(new Tuple<string, object, DateTime, DateTime>(key, val, DateTime.Now, DateTime.Now));
                    return true;

                    #endregion
                }
                else
                {
                    #region New-Entry
                    
                    Cache.Add(new Tuple<string, object, DateTime, DateTime>(key, val, DateTime.Now, DateTime.Now));
                    return true;

                    #endregion
                }
            }
            catch (Exception e)
            {
                LogException(e);
                throw e;
            }
            finally
            {
                Mtx.ReleaseMutex();
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

            DateTime startTime = DateTime.Now;
            Mtx.WaitOne();

            try
            {
                List<Tuple<string, object, DateTime, DateTime>> dupes = new List<Tuple<string, object, DateTime, DateTime>>();

                if (Cache.Count > 0) dupes = Cache.Where(x => x.Item1.ToLower() == key).ToList();
                else dupes = null;

                if (dupes == null) return true;
                else if (dupes.Count < 1) return true;
                else
                {
                    foreach (Tuple<string, object, DateTime, DateTime> curr in dupes)
                    {
                        Cache.Remove(curr);
                    }

                    return true;
                }
            }
            catch (Exception e)
            {
                LogException(e);
                throw e;
            }
            finally
            {
                Mtx.ReleaseMutex();
            }
        }

        private void Log(string message)
        {
            if (Debug)
            {
                Console.WriteLine(message);
            }
        }

        private void LogException(Exception e)
        {
            Log("================================================================================");
            Log("Exception Type: " + e.GetType().ToString());
            Log("Exception Data: " + e.Data);
            Log("Inner Exception: " + e.InnerException);
            Log("Exception Message: " + e.Message);
            Log("Exception Source: " + e.Source);
            Log("Exception StackTrace: " + e.StackTrace);
            Log("================================================================================");
        }
    }
}
