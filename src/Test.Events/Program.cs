using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Caching;
using GetSomeInput;
using Test.Shared;

namespace Test.Events
{
    partial class Program
    {
        static bool _RunForever = true;
        static readonly int _Capacity = 2048;
        static readonly int _EvictCount = 512;
        static readonly int _LoadCount = 256;
        static readonly int _DataLength = 4096;
        static ICache<string, string> _Cache = null;

        static void Main()
        {
            while (true)
            {
                string cacheType = Inputty.GetString("Cache type [lru|fifo]:", "lru", false);
                if (cacheType.Equals("lru"))
                {
                    _Cache = new LRUCache<string, string>(_Capacity, _EvictCount);
                    break;
                }
                else if (cacheType.Equals("fifo"))
                {
                    _Cache = new FIFOCache<string, string>(_Capacity, _EvictCount);
                    break;
                }
            }

            _Cache.Events.Evicted += Evicted;
            _Cache.Events.Expired += Expired;
            _Cache.Events.Prepopulated += Prepopulated;
            _Cache.Events.Replaced += Replaced;
            _Cache.Events.Removed += Removed;
            _Cache.Events.Disposed += Disposed;
            _Cache.Events.Cleared += Cleared;
            _Cache.Events.Added += Added;

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
                                foreach (KeyValuePair<string, string> entry in dump)
                                {
                                    Console.WriteLine(entry.Key + ": " + entry.Value);
                                }

                                Console.WriteLine("");
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
                                _Cache.AddReplace(loadKey, codeWord.Item2, DateTime.UtcNow.AddSeconds(10));
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

        private static void Replaced(object sender, DataEventArgs<string, string> e)
        {
            Console.WriteLine("*** Cache entry " + e.Key + " replaced");
        }

        private static void Removed(object sender, DataEventArgs<string, string> e)
        {
            Console.WriteLine("*** Cache entry " + e.Key + " removed");
        }

        private static void Prepopulated(object sender, DataEventArgs<string, string> e)
        {
            Console.WriteLine("*** Cache entry " + e.Key + " prepopulated from persistent storage");
        }

        private static void Evicted(object sender, List<string> e)
        {
            Console.WriteLine("*** Eviction event involving " + e.Count + " entries");
            foreach (string curr in e) Console.WriteLine("    | " + curr);
        }

        private static void Expired(object sender, string key)
        {
            Console.WriteLine("*** Expiration event, key " + key + " expired");
        }

        private static void Disposed(object sender, EventArgs e)
        {
            Console.WriteLine("*** Disposed");
        }

        private static void Cleared(object sender, EventArgs e)
        {
            Console.WriteLine("*** Cache cleared");
        }

        private static void Added(object sender, DataEventArgs<string, string> e)
        {
            Console.WriteLine("*** Cache entry " + e.Key + " added");
        }
    }
}