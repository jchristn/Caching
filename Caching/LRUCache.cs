using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Caching
{
    public class LRUCache
    {
        public int capacity;
        public int evict_count;
        public bool debug;

        private static readonly Mutex mutex = new Mutex();
        
        private List<Tuple<string, object, DateTime, DateTime>> cache { get; set; }
        // key, data, added, last_used

        public LRUCache(int capacity, int evict_count, bool debug)
        {
            this.capacity = capacity;
            this.evict_count = evict_count;
            this.debug = debug;
            this.cache = new List<Tuple<string, object, DateTime, DateTime>>();

            if (this.evict_count > this.capacity)
            {
                throw new ArgumentException("Evict count must be less than or equal to capacity.");
            }

            log("LRUCache initialized successfully with capacity " + capacity + " evict_count " + evict_count);
        }

        public int count()
        {
            DateTime start_time = DateTime.Now;
            mutex.WaitOne();
            log("LRUCache count acquired mutex");

            try
            {
                log("LRUCache count " + this.cache.Count);
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
                log("LRUCache count released mutex, exiting after " + total_time_from(start_time));
            }
        }
        
        public string oldest()
        {
            if (this.cache == null) return null;
            if (this.cache.Count < 1) return null;

            DateTime start_time = DateTime.Now;
            mutex.WaitOne();
            log("LRUCache oldest acquired mutex");

            try
            {
                Tuple<string, object, DateTime, DateTime> oldest = this.cache.Where(x => x.Item3 != null).OrderBy(x => x.Item3).First();
                log("LRUCache oldest key " + oldest.Item1 + ": " + oldest.Item3.ToString("MM/dd/yyyy hh:mm:ss"));
                return oldest.Item1;
            }
            catch (Exception e)
            {
                log_exception(e);
                throw e;
            }
            finally
            {
                mutex.ReleaseMutex();
                log("LRUCache oldest released mutex, exiting after " + total_time_from(start_time));
            }
        }

        public string newest()
        {
            if (this.cache == null) return null;
            if (this.cache.Count < 1) return null;

            DateTime start_time = DateTime.Now;
            mutex.WaitOne();
            log("LRUCache newest acquired mutex");

            try
            {
                Tuple<string, object, DateTime, DateTime> newest = this.cache.Where(x => x.Item3 != null).OrderBy(x => x.Item3).Last();
                log("LRUCache newest key " + newest.Item1 + ": " + newest.Item3.ToString("MM/dd/yyyy hh:mm:ss"));
                return newest.Item1;
            }
            catch (Exception e)
            {
                log_exception(e);
                throw e;
            }
            finally
            {
                mutex.ReleaseMutex();
                log("LRUCache newest released mutex, exiting after " + total_time_from(start_time));
            }
        }

        public string last_used()
        {
            if (this.cache == null) return null;
            if (this.cache.Count < 1) return null;

            DateTime start_time = DateTime.Now;
            mutex.WaitOne();
            log("LRUCache last_used acquired mutex");

            try
            {
                Tuple<string, object, DateTime, DateTime> newest = this.cache.Where(x => x.Item4 != null).OrderBy(x => x.Item4).Last();
                log("LRUCache last_used key " + newest.Item1 + ": " + newest.Item4.ToString("MM/dd/yyyy hh:mm:ss"));
                return newest.Item1;
            }
            catch (Exception e)
            {
                log_exception(e);
                throw e;
            }
            finally
            {
                mutex.ReleaseMutex();
                log("LRUCache last_used released mutex, exiting after " + total_time_from(start_time));
            }
        }

        public void clear()
        {
            DateTime start_time = DateTime.Now;
            mutex.WaitOne();
            log("LRUCache clear acquired mutex");

            try
            {
                this.cache = new List<Tuple<string, object, DateTime, DateTime>>();
                log("LRUCache clear successful");
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
                log("LRUCache clear released mutex, exiting after " + total_time_from(start_time));
            }
        }

        public object get(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException("key");

            DateTime start_time = DateTime.Now;
            mutex.WaitOne();
            log("LRUCache get acquired mutex");

            try
            {
                List<Tuple<string, object, DateTime, DateTime>> entries = new List<Tuple<string, object, DateTime, DateTime>>();

                if (cache.Count > 0) entries = cache.Where(x => x.Item1 == key).ToList();
                else entries = null;

                if (entries == null)
                {
                    #region No-Entries

                    log("LRUCache get no entries exist (null)");
                    return null;

                    #endregion
                }
                else
                {
                    #region Entries-Exist

                    if (entries.Count > 0)
                    {
                        log("LRUCache get " + entries.Count + " existing entries for key " + key + ", returning first");
                        foreach (Tuple<string, object, DateTime, DateTime> curr in entries)
                        {
                            cache.Remove(curr);
                            Tuple<string, object, DateTime, DateTime> curr_updated = new Tuple<string, object, DateTime, DateTime>(curr.Item1, curr.Item2, curr.Item3, DateTime.Now);
                            cache.Add(curr_updated);
                            return curr.Item2;
                        }
                    }
                    else
                    {
                        log("LRUCache get no entries found for key " + key + ", returning null");
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
                log("LRUCache get released mutex, exiting after " + total_time_from(start_time));
            }
        }

        public bool add_replace(string key, object val)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException("key");

            DateTime start_time = DateTime.Now;
            mutex.WaitOne();
            log("LRUCache add_replace acquired mutex");

            try
            {
                if (cache.Count >= capacity)
                {
                    #region Eviction

                    log("LRUCache add_replace cache full, evicting " + evict_count);
                    cache = cache.OrderBy(x => x.Item4).Skip(evict_count).ToList();
                    log("LRUCache add_replace cache full, eviction successful, " + cache.Count + " entries remain");

                    #endregion
                }

                List<Tuple<string, object, DateTime, DateTime>> dupes = new List<Tuple<string, object, DateTime, DateTime>>();

                if (cache.Count > 0) dupes = cache.Where(x => x.Item1.ToLower() == key).ToList();
                else dupes = null;

                if (dupes == null)
                {
                    #region New-Entry

                    log("LRUCache add_replace adding new entry for key " + key);
                    cache.Add(new Tuple<string, object, DateTime, DateTime>(key, val, DateTime.Now, DateTime.Now));
                    log("LRUCache add_replace key " + key + " added successfully");
                    return true;

                    #endregion
                }
                else if (dupes.Count > 0)
                {
                    #region Duplicate-Entries-Exist

                    log("LRUCache removing existing entries for key " + key);

                    foreach (Tuple<string, object, DateTime, DateTime> curr in dupes)
                    {
                        cache.Remove(curr);
                    }

                    log("LRUCache add_replace " + dupes.Count + " entries for key " + key + " removed successfully, adding new entry");
                    cache.Add(new Tuple<string, object, DateTime, DateTime>(key, val, DateTime.Now, DateTime.Now));
                    log("LRUCache add_replace key " + key + " added successfully");
                    return true;

                    #endregion
                }
                else
                {
                    #region New-Entry

                    log("LRUCache add_replace adding new entry for key " + key);
                    cache.Add(new Tuple<string, object, DateTime, DateTime>(key, val, DateTime.Now, DateTime.Now));
                    log("LRUCache add_replace key " + key + " added successfully");
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
                log("LRUCache add_replace released mutex, exiting after " + total_time_from(start_time));
            }
        }

        public bool remove(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException("key");

            DateTime start_time = DateTime.Now;
            mutex.WaitOne();
            log("LRUCache remove acquired mutex");

            try
            {
                List<Tuple<string, object, DateTime, DateTime>> dupes = new List<Tuple<string, object, DateTime, DateTime>>();

                if (cache.Count > 0) dupes = cache.Where(x => x.Item1.ToLower() == key).ToList();
                else dupes = null;

                if (dupes == null)
                {
                    #region No-Entries-NULL

                    log("LRUCache remove no entries for key " + key + " (null)");
                    return true;

                    #endregion
                }
                else if (dupes.Count < 1)
                {
                    #region No-Entries-EMPTY

                    log("LRUCache remove no entries for key " + key + " (empty)");
                    return true;

                    #endregion
                }
                else
                {
                    #region Entries-Exist

                    log("LRUCache remove " + dupes.Count + " entries for key " + key);

                    foreach (Tuple<string, object, DateTime, DateTime> curr in dupes)
                    {
                        cache.Remove(curr);
                    }

                    log("LRUCache remove " + dupes.Count + " entries for key " + key + " removed successfully");
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
                log("LRUCache remove released mutex, exiting after " + total_time_from(start_time));
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
