﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Caching;

namespace TestNetCore
{
    partial class Program
    {
        static void FIFOCacheTest()
        {
            try
            {
                bool runForever = true;
                int capacity = 2048;
                int evictCount = 512;
                int loadCount = 256; 
                int dataLen = 4096;
                byte[] data = InitByteArray(dataLen, 0x00);
                byte[] keyData;

                FIFOCache<string, byte[]> cache = new FIFOCache<string, byte[]>(capacity, evictCount);
                Thread.Sleep(250);

                while (runForever)
                {
                    Console.WriteLine("-------------------------------------------------------------------------------");
                    Console.WriteLine("Available commands (FIFO Cache Test):");
                    Console.WriteLine("  get              Get entry by key");
                    Console.WriteLine("  all              Get all entries");
                    Console.WriteLine("  load             Load " + loadCount + " new records");
                    Console.WriteLine("  oldest           Get the oldest entry");
                    Console.WriteLine("  newest           Get the newest entry");
                    Console.WriteLine("  count            Get the count of cached entries");
                    Console.WriteLine("  clear            Clear the cache");
                    Console.WriteLine("  quit             Exit the application");

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

                        case "all":
                            Dictionary<string, byte[]> dump = cache.All();
                            if (dump != null && dump.Count > 0)
                            {
                                foreach (KeyValuePair<string, byte[]> entry in dump) Console.WriteLine(entry.Key + ": " + Encoding.UTF8.GetString(entry.Value));
                            }
                            else
                            {
                                Console.WriteLine("Empty");
                            }
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
    }
}
