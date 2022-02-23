using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Caching;

namespace Test
{
    partial class Program
    {
        static void FIFOCacheTest()
        {
            try
            {
                bool runForever = true;
                byte[] data = InitByteArray(_DataLength, 0x00);
                byte[] keyData;

                FIFOCache<string, byte[]> cache = new FIFOCache<string, byte[]>(_Capacity, _EvictCount);
                Thread.Sleep(250);

                while (runForever)
                {
                    Console.Write("Command [FIFO] > ");
                    string userInput = Console.ReadLine();
                    if (String.IsNullOrEmpty(userInput)) continue;

                    switch (userInput.ToLower())
                    {
                        case "?":
                            MenuFIFO();
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

        static void MenuFIFO()
        {
            Console.WriteLine("Available commands:");
            Console.WriteLine("  get              Get entry by key");
            Console.WriteLine("  all              Get all entries");
            Console.WriteLine("  load             Load " + _LoadCount + " new records");
            Console.WriteLine("  oldest           Get the oldest entry");
            Console.WriteLine("  newest           Get the newest entry");
            Console.WriteLine("  count            Get the count of cached entries");
            Console.WriteLine("  clear            Clear the cache");
            Console.WriteLine("  quit             Exit the application");
            Console.WriteLine("");
        }
    }
}
