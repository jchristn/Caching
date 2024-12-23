﻿namespace Test.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices.ComTypes;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Caching;
    using GetSomeInput;
    using Test.Shared;

    partial class Program
    {
        static bool _RunForever = true;
        static readonly int _Capacity = 64;
        static readonly int _EvictCount = 16;
        static readonly int _LoadCount = 8;
        static readonly int _DataLength = 4096;
        static ICache<string, string> _Cache = null;

        static void Main()
        {
            PersistenceDriver persistence = new("./cache/");

            while (true)
            {
                string cacheType = Inputty.GetString("Cache type [lru|fifo]:", "lru", false);
                if (cacheType.Equals("lru"))
                {
                    _Cache = new LRUCache<string, string>(_Capacity, _EvictCount, persistence);
                    break;
                }
                else if (cacheType.Equals("fifo"))
                {
                    _Cache = new FIFOCache<string, string>(_Capacity, _EvictCount, persistence);
                    break;
                }
            }

            _Cache.Events.Evicted += EntriesEvicted;
            _Cache.Events.Prepopulated += EntryPrepopulated;
            _Cache.Prepopulate();

            try
            {
                byte[] data = Common.InitByteArray(_DataLength, 0x00);
                (int, string) codeWord;

                Thread.Sleep(250);

                while (_RunForever)
                {
                    string userInput = Inputty.GetString("Command [?/help]:", null, false);

                    switch (userInput.ToLower())
                    {
                        case "?":
                            Menu();
                            break;

                        case "get":
                            string getKey = Inputty.GetString("Key:", null, true);
                            if (String.IsNullOrEmpty(getKey)) break;
                            if (_Cache.TryGet(getKey, out string keyData))
                            {
                                Console.WriteLine("Cache hit: " + keyData);
                                codeWord = Common.GetCodeWord(getKey);
                                Console.WriteLine("Expected (" + codeWord.Item1 + "): " + codeWord.Item2);
                            }
                            else
                            {
                                Console.WriteLine("Cache miss");
                            }
                            break;

                        case "all":
                            Dictionary<string, string> dump = _Cache.All();
                            if (dump != null && dump.Count > 0)
                            {
                                foreach (KeyValuePair<string, string> entry in dump) Console.WriteLine(entry.Key + ": " + entry.Value);
                                Console.WriteLine("Count: " + dump.Count + " entries");
                            }
                            else
                            {
                                Console.WriteLine("Empty");
                            }
                            break;

                        case "load":
                            DateTime startTime = DateTime.Now;

                            for (int i = 1; i <= _LoadCount; i++)
                            {
                                string loadKey = Guid.NewGuid().ToString();
                                codeWord = Common.GetCodeWord(loadKey);
                                
                                Console.WriteLine("Adding entry " + i + " of " + _LoadCount + ": " + loadKey + " (" + codeWord.Item1 + ") " + codeWord.Item2 + "                      \r");
                                _Cache.AddReplace(loadKey, codeWord.Item2);
                            }

                            Console.WriteLine(
                                Environment.NewLine 
                                + "Loaded " 
                                + _LoadCount 
                                + " records in " 
                                + Common.TotalTimeFrom(startTime) 
                                + ": " 
                                + Common.DecimalToString(Common.TotalMsFrom(startTime) / _LoadCount) 
                                + "ms per entry");
                            break;

                        case "oldest":
                            Console.WriteLine("Oldest key: " + _Cache.Oldest());
                            break;

                        case "newest":
                            Console.WriteLine("Newest key: " + _Cache.Newest());
                            break;

                        case "count":
                            Console.WriteLine("Cache count: " + _Cache.Count());
                            break;

                        case "clear":
                            _Cache.Clear();
                            Console.WriteLine("Cache cleared");
                            break;

                        case "q":
                        case "quit":
                            _RunForever = false;
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
                Common.PrintException(e);
            }
            finally
            {
                Console.WriteLine("");
                Console.Write("Press ENTER to exit.");
                Console.ReadLine();
            }
        }

        static void Menu()
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

        static void EntriesEvicted(object sender, List<string> e)
        {
            if (e != null && e.Count > 0)
            {
                Console.WriteLine(e.Count + " entries evicted:");
                foreach (string s in e) Console.WriteLine("| " + s);
            }
        }

        static void EntryPrepopulated(object sender, DataEventArgs<string, string> e)
        {
            Console.WriteLine("Prepopulated " + e.Key + ": " + e.Data.Data);
        }
    }
}