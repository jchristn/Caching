﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Caching;

namespace CachingTest
{
    partial class Program
    {
        static void FIFOCacheTest()
        {
            try
            {
                bool run_forever = true;
                int capacity = 2048;
                int evict_count = 512;
                int load_count = 256;
                bool cache_debug = false;
                int byte_array_len = 4096;
                byte[] data = init_byte_array(byte_array_len, 0x00);

                FIFOCache cache = new FIFOCache(capacity, evict_count, cache_debug);

                while (run_forever)
                {
                    Console.WriteLine("-------------------------------------------------------------------------------");
                    Console.WriteLine("Available commands (FIFO Cache Test):");
                    Console.WriteLine("  get              Get entry by key");
                    Console.WriteLine("  load             Load " + load_count + " new records");
                    Console.WriteLine("  oldest           Get the oldest entry");
                    Console.WriteLine("  newest           Get the newest entry");
                    Console.WriteLine("  count            Get the count of cached entries");
                    Console.WriteLine("  clear            Clear the cache");
                    Console.WriteLine("  quit             Exit the application");
                    Console.WriteLine("  debug            Flip the cache debug flag (currently " + cache.debug + ")");

                    Console.WriteLine("");
                    Console.Write("Command > ");
                    string user_input = Console.ReadLine();
                    if (String.IsNullOrEmpty(user_input)) continue;

                    switch (user_input.ToLower())
                    {
                        case "get":
                            Console.Write("Key > ");
                            string get_key = Console.ReadLine();
                            if (String.IsNullOrEmpty(get_key)) break;
                            byte[] key_data = (byte[])cache.get(get_key);
                            if (key_data == null) Console.WriteLine("Cache miss");
                            else Console.WriteLine("Cache hit");
                            break;

                        case "load":
                            DateTime start_time = DateTime.Now;

                            for (int i = 0; i < load_count; i++)
                            {
                                string load_key = Guid.NewGuid().ToString();
                                Console.Write("Adding entry " + i + " of " + load_count + "                      \r");
                                cache.add_replace(load_key, data);
                            }

                            Console.WriteLine(
                                "Loaded " + load_count +
                                " records in " + total_time_from(start_time) + ": " +
                                decimal_tostring(total_ms_from(start_time) / load_count) + "ms per entry");
                            break;

                        case "oldest":
                            Console.WriteLine("Oldest key: " + cache.oldest());
                            break;

                        case "newest":
                            Console.WriteLine("Newest key: " + cache.newest());
                            break;

                        case "count":
                            Console.WriteLine("Cache count: " + cache.count());
                            break;

                        case "clear":
                            cache.clear();
                            Console.WriteLine("Cache cleared");
                            break;

                        case "q":
                        case "quit":
                            run_forever = false;
                            break;

                        case "debug":
                            cache.debug = !cache.debug;
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
                print_exception(e);
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
