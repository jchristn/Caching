using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Caching;

namespace Test.Events
{
    partial class Program
    {
        static void LRUCacheTest()
        {
            try
            {
                bool runForever = true;
                byte[] data = InitByteArray(_DataLength, 0x00);
                byte[] keyData;

                LRUCache<string, byte[]> cache = new LRUCache<string, byte[]>(_Capacity, _EvictCount);
                cache.Events.Added += Added;
                cache.Events.Cleared += Cleared;
                cache.Events.Disposed += Disposed;
                cache.Events.Evicted += Evicted;
                cache.Events.Prepopulated += Prepopulated;
                cache.Events.Removed += Removed;
                cache.Events.Replaced += Replaced;

                Thread.Sleep(250);

                while (runForever)
                {
                    Console.Write("Command [LRU] > ");
                    string userInput = Console.ReadLine();
                    if (String.IsNullOrEmpty(userInput)) continue;

                    switch (userInput.ToLower())
                    {
                        case "?":
                            MenuLRU();
                            break;

                        case "get":
                            Console.Write("Key > ");
                            string getKey = Console.ReadLine();
                            if (String.IsNullOrEmpty(getKey)) break;
                            if (cache.TryGet(getKey, out keyData)) Console.WriteLine("Cache hit");
                            else Console.WriteLine("Cache miss");
                            break;

                        case "all":
                            Dictionary<string, byte[]> dump = cache.All();
                            if (dump != null && dump.Count > 0)
                            {
                                foreach (KeyValuePair<string, byte[]> entry in dump) Console.WriteLine(entry.Key);
                                Console.WriteLine("Count: " + dump.Count + " entries");
                            }
                            else
                            {
                                Console.WriteLine("Empty");
                            }
                            break;

                        case "add":
                            Console.Write("Key  : ");
                            string key = Console.ReadLine();
                            if (String.IsNullOrEmpty(key)) break;
                            Console.Write("Data : ");
                            string newData = Console.ReadLine();
                            if (String.IsNullOrEmpty(newData)) break;
                            byte[] newDataBytes = Encoding.UTF8.GetBytes(newData);
                            cache.AddReplace(key, newDataBytes);
                            break;

                        case "load":
                            DateTime startTime = DateTime.Now;

                            for (int i = 0; i < _LoadCount; i++)
                            {
                                string loadKey = Guid.NewGuid().ToString();
                                Console.Write("Adding entry " + i + " of " + _LoadCount + "                      \r");
                                cache.AddReplace(loadKey, data);
                            }

                            Console.WriteLine(
                                "Loaded " + _LoadCount +
                                " records in " + TotalTimeFrom(startTime) + ": " +
                                DecimalToString(TotalMsFrom(startTime) / _LoadCount) + "ms per entry");
                            break;

                        case "last_used":
                            Console.WriteLine("Last used key: " + cache.LastUsed());
                            break;

                        case "first_used":
                            Console.WriteLine("First used key: " + cache.FirstUsed());
                            break;

                        case "oldest":
                            Console.WriteLine("Oldest key: " + cache.Oldest());
                            break;

                        case "newest":
                            Console.WriteLine("Newest key: " + cache.Newest());
                            break;

                        case "count":
                            Console.WriteLine("Cache count: " + cache.Count());
                            break;

                        case "clear":
                            cache.Clear();
                            Console.WriteLine("Cache cleared");
                            break;

                        case "dispose":
                            cache.Dispose();
                            break;

                        case "q":
                        case "quit":
                            runForever = false;
                            break;
                             
                        default:
                            continue;
                    }
                }

                Console.WriteLine("Goodbye!");
                return;
            }
            catch (Exception e)
            {
                PrintException(e);
            }
            finally
            {
                Console.WriteLine("");
                Console.Write("Press ENTER to exit.");
                Console.ReadLine();
            }
        }

        static void MenuLRU()
        {
            Console.WriteLine("Available commands:");
            Console.WriteLine("  get              Get entry by key");
            Console.WriteLine("  all              Get all entries");
            Console.WriteLine("  add              Add a record");
            Console.WriteLine("  load             Load " + _LoadCount + " new records");
            Console.WriteLine("  last_used        Get the last used entry");
            Console.WriteLine("  first_used       Get the first used entry");
            Console.WriteLine("  oldest           Get the oldest entry");
            Console.WriteLine("  newest           Get the newest entry");
            Console.WriteLine("  count            Get the count of cached entries");
            Console.WriteLine("  clear            Clear the cache");
            Console.WriteLine("  dispose          Dispose of the cache");
            Console.WriteLine("  quit             Exit the application");
            Console.WriteLine("");
        }
    }
}