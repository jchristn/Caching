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
    public class LRUCacheBTree
    {
        public int capacity;
        public int evict_count;
        public bool debug;

        private static readonly Mutex mutex = new Mutex();

        // private List<Tuple<string, object, DateTime, DateTime>> cache { get; set; }
        private BPlusTree<string, Tuple<object, DateTime, DateTime>> cache = new BPlusTree<string, Tuple<object, DateTime, DateTime>>();
        // key, data, added, last_used

        public LRUCacheBTree(int capacity, int evict_count, bool debug)
        {
            this.capacity = capacity;
            this.evict_count = evict_count;
            this.debug = debug;
            this.cache = new BPlusTree<string, Tuple<object, DateTime, DateTime>>();
            this.cache.EnableCount();

            if (this.evict_count > this.capacity)
            {
                throw new ArgumentException("Evict count must be less than or equal to capacity.");
            }

            log("BTreeLRUCache initialized successfully with capacity " + capacity + " evict_count " + evict_count);
        }

        public int count()
        {
            DateTime start_time = DateTime.Now;
            mutex.WaitOne();
            log("BTreeLRUCache count acquired mutex");

            try
            {
                log("BTreeLRUCache count " + this.cache.Count);
                return this.cache.Count;
            }
            catch (Exception e)
            {
                log_exception(e);
                throw e;
            }
            finally
            {
                mutex.ReleaseMutex();
                log("BTreeLRUCache count released mutex, exiting after " + total_time_from(start_time));
            }
        }
        
        public string oldest()
        {
            if (this.cache == null) return null;
            if (this.cache.Count < 1) return null;

            DateTime start_time = DateTime.Now;
            mutex.WaitOne();
            log("BTreeLRUCache oldest acquired mutex");

            try
            {
                // key, Tuple<data, added, last_used>
                KeyValuePair<string, Tuple<object, DateTime, DateTime>> oldest = this.cache.Where(x => x.Value.Item2 != null).OrderBy(x => x.Value.Item2).First();
                log("BTreeLRUCache oldest key " + oldest.Key + ": " + oldest.Value.Item2.ToString("MM/dd/yyyy hh:mm:ss"));
                return oldest.Key;
            }
            catch (Exception e)
            {
                log_exception(e);
                throw e;
            }
            finally
            {
                mutex.ReleaseMutex();
                log("BTreeLRUCache oldest released mutex, exiting after " + total_time_from(start_time));
            }
        }

        public string newest()
        {
            if (this.cache == null) return null;
            if (this.cache.Count < 1) return null;

            DateTime start_time = DateTime.Now;
            mutex.WaitOne();
            log("BTreeLRUCache newest acquired mutex");

            try
            {                
                // key, Tuple<data, added, last_used>
                KeyValuePair<string, Tuple<object, DateTime, DateTime>> newest = this.cache.Where(x => x.Value.Item2 != null).OrderBy(x => x.Value.Item2).Last();
                log("BTreeLRUCache newest key " + newest.Key + ": " + newest.Value.Item2.ToString("MM/dd/yyyy hh:mm:ss"));
                return newest.Key;
            }
            catch (Exception e)
            {
                log_exception(e);
                throw e;
            }
            finally
            {
                mutex.ReleaseMutex();
                log("BTreeLRUCache newest released mutex, exiting after " + total_time_from(start_time));
            }
        }

        public string last_used()
        {
            if (this.cache == null) return null;
            if (this.cache.Count < 1) return null;

            DateTime start_time = DateTime.Now;
            mutex.WaitOne();
            log("BTreeLRUCache last_used acquired mutex");

            try
            {
                // key, Tuple<data, added, last_used>
                KeyValuePair<string, Tuple<object, DateTime, DateTime>> newest = this.cache.Where(x => x.Value.Item2 != null).OrderBy(x => x.Value.Item3).Last();
                log("BTreeLRUCache last_used key " + newest.Key + ": " + newest.Value.Item3.ToString("MM/dd/yyyy hh:mm:ss"));
                return newest.Key;
            }
            catch (Exception e)
            {
                log_exception(e);
                throw e;
            }
            finally
            {
                mutex.ReleaseMutex();
                log("BTreeLRUCache last_used released mutex, exiting after " + total_time_from(start_time));
            }
        }

        public string first_used()
        {
            if (this.cache == null) return null;
            if (this.cache.Count < 1) return null;

            DateTime start_time = DateTime.Now;
            mutex.WaitOne();
            log("BTreeLRUCache first_used acquired mutex");

            try
            {
                // key, Tuple<data, added, last_used>
                KeyValuePair<string, Tuple<object, DateTime, DateTime>> oldest = this.cache.Where(x => x.Value.Item2 != null).OrderBy(x => x.Value.Item3).First();
                log("BTreeLRUCache first_used key " + oldest.Key + ": " + oldest.Value.Item3.ToString("MM/dd/yyyy hh:mm:ss"));
                return oldest.Key;
            }
            catch (Exception e)
            {
                log_exception(e);
                throw e;
            }
            finally
            {
                mutex.ReleaseMutex();
                log("BTreeLRUCache first_used released mutex, exiting after " + total_time_from(start_time));
            }
        }

        public void clear()
        {
            DateTime start_time = DateTime.Now;
            mutex.WaitOne();
            log("BTreeLRUCache clear acquired mutex");

            try
            {
                this.cache = new BPlusTree<string, Tuple<object, DateTime, DateTime>>();
                this.cache.EnableCount();
                log("BTreeLRUCache clear successful");
                return;
            }
            catch (Exception e)
            {
                log_exception(e);
                throw e;
            }
            finally
            {
                mutex.ReleaseMutex();
                log("BTreeLRUCache clear released mutex, exiting after " + total_time_from(start_time));
            }
        }

        public object get(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException("key");

            DateTime start_time = DateTime.Now;
            mutex.WaitOne();
            log("BTreeLRUCache get acquired mutex");

            try
            {
                // key, Tuple<data, added, last_used>
                List<KeyValuePair<string, Tuple<object, DateTime, DateTime>>> entries = new List<KeyValuePair<string, Tuple<object, DateTime, DateTime>>>();
                
                if (cache.Count > 0) entries = cache.Where(x => x.Key == key).ToList();
                else entries = null;

                if (entries == null)
                {
                    #region No-Entries

                    log("BTreeLRUCache get no entries exist (null)");
                    return null;

                    #endregion
                }
                else
                {
                    #region Entries-Exist

                    if (entries.Count > 0)
                    {
                        log("BTreeLRUCache get " + entries.Count + " existing entries for key " + key + ", returning first");
                        foreach (KeyValuePair<string, Tuple<object, DateTime, DateTime>> curr in entries)
                        {
                            cache.Remove(curr.Key);
                            Tuple<object, DateTime, DateTime> val = new Tuple<object, DateTime, DateTime>(curr.Value.Item1, curr.Value.Item2, DateTime.Now);
                            cache.Add(curr.Key, val);
                            return curr.Key;
                        }
                    }
                    else
                    {
                        log("BTreeLRUCache get no entries found for key " + key + ", returning null");
                    }

                    return null;

                    #endregion
                }
            }
            catch (Exception e)
            {
                log_exception(e);
                throw e;
            }
            finally
            {
                mutex.ReleaseMutex();
                log("BTreeLRUCache get released mutex, exiting after " + total_time_from(start_time));
            }
        }

        public bool add_replace(string key, object val)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException("key");

            DateTime start_time = DateTime.Now;
            mutex.WaitOne();
            log("BTreeLRUCache add_replace acquired mutex");

            try
            {
                if (cache.Count >= capacity)
                {
                    #region Eviction

                    log("BTreeLRUCache add_replace cache full, evicting " + evict_count);

                    int evicted_count = 0;
                    while (evicted_count < evict_count)
                    {
                        KeyValuePair<string, Tuple<object, DateTime, DateTime>> oldest = this.cache.Where(x => x.Value.Item2 != null).OrderBy(x => x.Value.Item3).First();
                        cache.Remove(oldest.Key);
                        evicted_count++;

                        log("BTreeLRUCache add_replace evicted key " + oldest.Key + " (" + evicted_count + " of " + evict_count + ")");
                    }

                    log("BTreeLRUCache add_replace cache full, eviction successful, " + cache.Count + " entries remain");

                    #endregion
                }

                List<KeyValuePair<string, Tuple<object, DateTime, DateTime>>> dupes = new List<KeyValuePair<string, Tuple<object, DateTime, DateTime>>>();
                if (cache.Count > 0) dupes = cache.Where(x => x.Key.ToLower() == key).ToList();
                else dupes = null;

                if (dupes == null)
                {
                    #region New-Entry

                    log("BTreeLRUCache add_replace adding new entry for key " + key);
                    Tuple<object, DateTime, DateTime> value = new Tuple<object, DateTime, DateTime>(val, DateTime.Now, DateTime.Now);
                    cache.Add(key, value);
                    log("BTreeLRUCache add_replace key " + key + " added successfully");
                    return true;

                    #endregion
                }
                else if (dupes.Count > 0)
                {
                    #region Duplicate-Entries-Exist

                    log("BTreeLRUCache removing existing entries for key " + key);

                    foreach (KeyValuePair<string, Tuple<object, DateTime, DateTime>> curr in dupes)
                    {
                        cache.Remove(curr.Key);
                    }

                    log("BTreeLRUCache add_replace " + dupes.Count + " entries for key " + key + " removed successfully, adding new entry");
                    Tuple<object, DateTime, DateTime> value = new Tuple<object, DateTime, DateTime>(val, DateTime.Now, DateTime.Now);
                    cache.Add(key, value);
                    log("BTreeLRUCache add_replace key " + key + " added successfully");
                    return true;

                    #endregion
                }
                else
                {
                    #region New-Entry

                    log("BTreeLRUCache add_replace adding new entry for key " + key);
                    Tuple<object, DateTime, DateTime> value = new Tuple<object, DateTime, DateTime>(val, DateTime.Now, DateTime.Now);
                    cache.Add(key, value);
                    log("BTreeLRUCache add_replace key " + key + " added successfully");
                    return true;

                    #endregion
                }
            }
            catch (Exception e)
            {
                log_exception(e);
                throw e;
            }
            finally
            {
                mutex.ReleaseMutex();
                log("BTreeLRUCache add_replace released mutex, exiting after " + total_time_from(start_time));
            }
        }

        public bool remove(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException("key");

            DateTime start_time = DateTime.Now;
            mutex.WaitOne();
            log("BTreeLRUCache remove acquired mutex");

            try
            {
                List<KeyValuePair<string, Tuple<object, DateTime, DateTime>>> dupes = new List<KeyValuePair<string, Tuple<object, DateTime, DateTime>>>();

                if (cache.Count > 0) dupes = cache.Where(x => x.Key.ToLower() == key).ToList();
                else dupes = null;

                if (dupes == null)
                {
                    #region No-Entries-NULL

                    log("BTreeLRUCache remove no entries for key " + key + " (null)");
                    return true;

                    #endregion
                }
                else if (dupes.Count < 1)
                {
                    #region No-Entries-EMPTY

                    log("BTreeLRUCache remove no entries for key " + key + " (empty)");
                    return true;

                    #endregion
                }
                else
                {
                    #region Entries-Exist

                    log("BTreeLRUCache remove " + dupes.Count + " entries for key " + key);

                    foreach (KeyValuePair<string, Tuple<object, DateTime, DateTime>> curr in dupes)
                    {
                        cache.Remove(curr.Key);
                    }

                    log("BTreeLRUCache remove " + dupes.Count + " entries for key " + key + " removed successfully");
                    return true;

                    #endregion
                }
            }
            catch (Exception e)
            {
                log_exception(e);
                throw e;
            }
            finally
            {
                mutex.ReleaseMutex();
                log("BTreeLRUCache remove released mutex, exiting after " + total_time_from(start_time));
            }
        }

        private void log(string message)
        {
            if (debug)
            {
                Console.WriteLine(message);
            }
        }

        private void log_exception(Exception e)
        {
            log("================================================================================");
            log("Exception Type: " + e.GetType().ToString());
            log("Exception Data: " + e.Data);
            log("Inner Exception: " + e.InnerException);
            log("Exception Message: " + e.Message);
            log("Exception Source: " + e.Source);
            log("Exception StackTrace: " + e.StackTrace);
            log("================================================================================");
        }

        private static string total_time_from(DateTime start_time)
        {
            DateTime end_time = DateTime.Now;
            TimeSpan total_time = (end_time - start_time);

            if (total_time.TotalDays > 1)
            {
                return decimal_tostring(total_time.TotalDays) + "days";
            }
            else if (total_time.TotalHours > 1)
            {
                return decimal_tostring(total_time.TotalHours) + "hrs";
            }
            else if (total_time.TotalMinutes > 1)
            {
                return decimal_tostring(total_time.TotalMinutes) + "mins";
            }
            else if (total_time.TotalSeconds > 1)
            {
                return decimal_tostring(total_time.TotalSeconds) + "sec";
            }
            else if (total_time.TotalMilliseconds > 0)
            {
                return decimal_tostring(total_time.TotalMilliseconds) + "ms";
            }
            else
            {
                return "<unknown>";
            }
        }

        private static double total_ms_from(DateTime start_time)
        {
            try
            {
                DateTime end_time = DateTime.Now;
                TimeSpan total_time = (end_time - start_time);
                return total_time.TotalMilliseconds;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        private static string decimal_tostring(object obj)
        {
            if (obj == null) return null;
            if (obj is string) if (String.IsNullOrEmpty(obj.ToString())) return null;
            string ret = string.Format("{0:N2}", obj);
            ret = ret.Replace(",", "");
            return ret;
        }
    }
}
