using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Caching;

namespace CachingTest
{
    partial class Program
    {
        static void LRUCacheTest()
        {
            try
            {
                bool runForever = true;
                int capacity = 2048;
                int evictCount = 512;
                int loadCount = 256;
                bool cacheDebug = true;
                int dataLen = 4096;
                byte[] data = InitByteArray(dataLen, 0x00);
                byte[] keyData;

                LRUCache<byte[]> cache = new LRUCache<byte[]>(capacity, evictCount, cacheDebug);
                Thread.Sleep(250);

                while (runForever)
                {
                    Console.WriteLine("-------------------------------------------------------------------------------");
                    Console.WriteLine("Available commands (LRU Cache Test):");
                    Console.WriteLine("  get              Get entry by key");
                    Console.WriteLine("  load             Load " + loadCount + " new records");
                    Console.WriteLine("  last_used        Get the last used entry");
                    Console.WriteLine("  first_used       Get the first used entry");
                    Console.WriteLine("  oldest           Get the oldest entry");
                    Console.WriteLine("  newest           Get the newest entry");
                    Console.WriteLine("  count            Get the count of cached entries");
                    Console.WriteLine("  clear            Clear the cache");
                    Console.WriteLine("  quit             Exit the application");
                    Console.WriteLine("  debug            Flip the cache debug flag (currently " + cache.Debug + ")");

                    Console.WriteLine("");
                    Console.Write("Command > ");
                    string userInput = Console.ReadLine();
                    if (String.IsNullOrEmpty(userInput)) continue;

                    switch (userInput.ToLower())
                    {
                        case "get":
                            Console.Write("Key > ");
                            string getKey = Console.ReadLine();
                            if (String.IsNullOrEmpty(getKey)) break;
                            if (cache.TryGet(getKey, out keyData)) Console.WriteLine("Cache hit");
                            else Console.WriteLine("Cache miss");
                            break;

                        case "load":
                            DateTime startTime = DateTime.Now;

                            for (int i = 0; i < loadCount; i++)
                            {
                                string loadKey = Guid.NewGuid().ToString();
                                Console.Write("Adding entry " + i + " of " + loadCount + "                      \r");
                                cache.AddReplace(loadKey, data);
                            }

                            Console.WriteLine(
                                "Loaded " + loadCount +
                                " records in " + TotalTimeFrom(startTime) + ": " +
                                DecimalToString(TotalMsFrom(startTime) / loadCount) + "ms per entry");
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

                        case "q":
                        case "quit":
                            runForever = false;
                            break;

                        case "debug":
                            cache.Debug = !cache.Debug;
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
    }
}