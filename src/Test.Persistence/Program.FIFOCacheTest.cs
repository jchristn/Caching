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
        static void FIFOCacheTest()
        {
            try
            {
                bool runForever = true;
                byte[] data = InitByteArray(_DataLength, 0x00);
                byte[] keyData;

                Persistence persistence = new Persistence("./cache");

                FIFOCache<string, byte[]> cache = new FIFOCache<string, byte[]>(_Capacity, _EvictCount, persistence);
                Thread.Sleep(250);

                while (runForever)
                {
                    Console.Write("Command > ");
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

    public class Persistence : PersistenceDriver
    {
        private string _BaseDirectory = null;

        public Persistence(string baseDir)
        {
            if (String.IsNullOrEmpty(baseDir)) throw new ArgumentNullException(nameof(baseDir));
            baseDir = baseDir.Replace("\\", "/");
            if (!baseDir.EndsWith("/")) baseDir += "/";
            if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);
            _BaseDirectory = baseDir;
        }

        public override void Delete(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            key = GenerateKey(key);
            File.Delete(key);
        }

        public override bool Exists(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            key = GenerateKey(key);
            return File.Exists(key);
        }

        public override byte[] Get(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            key = GenerateKey(key);
            return File.ReadAllBytes(key);
        }

        public override void Write(string key, byte[] data)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (data == null) data = new byte[0];
            key = GenerateKey(key);
            File.WriteAllBytes(key, data);
        }

        public override byte[] ToBytes(object data)
        {
            if (data == null) return new byte[0];
            return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data));
        }

        public override T FromBytes<T>(byte[] data)
        {
            ReadOnlySpan<byte> bytes = new ReadOnlySpan<byte>(data);
            return JsonSerializer.Deserialize<T>(bytes);
        }

        public override List<string> Enumerate()
        {
            IEnumerable<string> files = Directory.EnumerateFiles(_BaseDirectory, "*.*", SearchOption.TopDirectoryOnly);

            if (files != null)
            {
                List<string> updated = new List<string>();

                foreach (string file in new List<string>(files))
                {
                    updated.Add(file.Replace(_BaseDirectory, ""));
                }

                files = updated;
            }

            return new List<string>(files);
        }

        private string GenerateKey(string key)
        {
            return _BaseDirectory + key;
        }
    }
}
