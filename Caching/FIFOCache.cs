using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Caching
{
    public class FIFOCache
    {
        public int capacity;
        public int evict_count;
        public bool debug;

        private static readonly Mutex mutex = new Mutex();
        
        private List<Tuple<string, object, DateTime>> cache { get; set; }

        public FIFOCache(int capacity, int evict_count, bool debug)
        {
            this.capacity = capacity;
            this.evict_count = evict_count;
            this.debug = debug;
            this.cache = new List<Tuple<string, object, DateTime>>();

            if (this.evict_count > this.capacity)
            {
                throw new ArgumentException("Evict count must be less than or equal to capacity.");
            }

            log("FIFOCache initialized successfully with capacity " + capacity + " evict_count " + evict_count);
        }

        public int count()
        {
            log("FIFOCache count " + this.cache.Count);
            return this.cache.Count;
        }

        public string oldest()
        {
            if (this.cache == null) return null;
            if (this.cache.Count < 1) return null;
            Tuple<string, object, DateTime> oldest = this.cache.Where(x => x.Item3 != null).OrderBy(x => x.Item3).First();
            log("FIFOCache oldest key " + oldest.Item1 + ": " + oldest.Item3.ToString("MM/dd/yyyy hh:mm:ss"));
            return oldest.Item1;
        }

        public string newest()
        {
            if (this.cache == null) return null;
            if (this.cache.Count < 1) return null;
            Tuple<string, object, DateTime> newest = this.cache.Where(x => x.Item3 != null).OrderBy(x => x.Item3).Last();
            log("FIFOCache newest key " + newest.Item1 + ": " + newest.Item3.ToString("MM/dd/yyyy hh:mm:ss"));
            return newest.Item1;
        }

        public void clear()
        {
            this.cache = new List<Tuple<string, object, DateTime>>();
            log("FIFOCache clear successful");
            return;
        }

        public object get(string key)
        {
            DateTime start_time = DateTime.Now;
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException("key");

            log("FIFOCache get key " + key + " waiting on mutex");

            mutex.WaitOne();
            log("FIFOCache get acquired mutex");

            try
            {
                List<Tuple<string, object, DateTime>> entries = new List<Tuple<string, object, DateTime>>();

                if (cache.Count > 0) entries = cache.Where(x => x.Item1 == key).ToList();
                else entries = null;

                if (entries == null)
                {
                    #region No-Entries

                    log("FIFOCache get no entries exist (null)");
                    return null;

                    #endregion
                }
                else
                {
                    #region Entries-Exist

                    if (entries.Count > 0)
                    {
                        log("FIFOCache get " + entries.Count + " existing entries for key " + key + ", returning first");
                        foreach (Tuple<string, object, DateTime> curr in entries)
                        {
                            return curr.Item2;
                        }
                    }
                    else
                    {
                        log("FIFOCache get no entries found for key " + key + ", returning null");
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
                log("FIFOCache get released mutex, exiting after " + total_time_from(start_time));
            }

        }

        public bool add_replace(string key, object val)
        {
            DateTime start_time = DateTime.Now;
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException("key");

            log("FIFOCache add_replace key " + key + " waiting on mutex");

            mutex.WaitOne();
            log("FIFOCache add_replace acquired mutex");

            try
            {
                if (cache.Count >= capacity)
                {
                    #region Eviction

                    log("FIFOCache add_replace cache full, evicting " + evict_count);
                    cache = cache.OrderBy(x => x.Item3).Skip(evict_count).ToList();
                    log("FIFOCache add_replace cache full, eviction successful, " + cache.Count + " entries remain");

                    #endregion
                }

                List<Tuple<string, object, DateTime>> dupes = new List<Tuple<string, object, DateTime>>();

                if (cache.Count > 0) dupes = cache.Where(x => x.Item1.ToLower() == key).ToList();
                else dupes = null;

                if (dupes == null)
                {
                    #region New-Entry

                    log("FIFOCache add_replace adding new entry for key " + key);
                    cache.Add(new Tuple<string, object, DateTime>(key, val, DateTime.Now));
                    log("FIFOCache add_replace key " + key + " added successfully");
                    return true;

                    #endregion
                }
                else if (dupes.Count > 0)
                {
                    #region Duplicate-Entries-Exist

                    log("FIFOCache removing existing entries for key " + key);

                    foreach (Tuple<string, object, DateTime> curr in dupes)
                    {
                        cache.Remove(curr);
                    }

                    log("FIFOCache add_replace " + dupes.Count + " entries for key " + key + " removed successfully, adding new entry");
                    cache.Add(new Tuple<string, object, DateTime>(key, val, DateTime.Now));
                    log("FIFOCache add_replace key " + key + " added successfully");
                    return true;

                    #endregion
                }
                else
                {
                    #region New-Entry

                    log("FIFOCache add_replace adding new entry for key " + key);
                    cache.Add(new Tuple<string, object, DateTime>(key, val, DateTime.Now));
                    log("FIFOCache add_replace key " + key + " added successfully");
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
                log("FIFOCache add_replace released mutex, exiting after " + total_time_from(start_time));
            }
        }

        public bool remove(string key)
        {
            DateTime start_time = DateTime.Now;
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException("key");

            log("FIFOCache remove key " + key + " waiting on mutex");

            mutex.WaitOne();
            log("FIFOCache remove acquired mutex");

            try
            {
                List<Tuple<string, object, DateTime>> dupes = new List<Tuple<string, object, DateTime>>();

                if (cache.Count > 0) dupes = cache.Where(x => x.Item1.ToLower() == key).ToList();
                else dupes = null;

                if (dupes == null)
                {
                    #region No-Entries-NULL

                    log("FIFOCache remove no entries for key " + key + " (null)");
                    return true;

                    #endregion
                }
                else if (dupes.Count < 1)
                {
                    #region No-Entries-EMPTY

                    log("FIFOCache remove no entries for key " + key + " (empty)");
                    return true;

                    #endregion
                }
                else
                {
                    #region Entries-Exist

                    log("FIFOCache remove " + dupes.Count + " entries for key " + key);

                    foreach (Tuple<string, object, DateTime> curr in dupes)
                    {
                        cache.Remove(curr);
                    }

                    log("FIFOCache remove " + dupes.Count + " entries for key " + key + " removed successfully");
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
                log("FIFOCache remove released mutex, exiting after " + total_time_from(start_time));
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
