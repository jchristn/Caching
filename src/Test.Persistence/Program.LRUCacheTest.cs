using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using Caching;

namespace Test.Persistence
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

                Persistence persistence = new Persistence("./cache");

                LRUCache<string, byte[]> cache = new LRUCache<string, byte[]>(_Capacity, _EvictCount, persistence);
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
            Console.WriteLine("  ?                Help, this menu");
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
