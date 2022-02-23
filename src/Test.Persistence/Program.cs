using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Caching;

namespace Test.Persistence
{
    partial class Program
    {
        static int _Capacity = 64;
        static int _EvictCount = 16;
        static int _LoadCount = 8;
        static int _DataLength = 4096;

        static void Main(string[] args)
        {
            bool runForever = true;
            while (runForever)
            {
                Console.WriteLine("-------------------------------------------------------------------------------");
                Console.WriteLine("Select which cache you wish to test");
                Console.WriteLine("  fifo");
                Console.WriteLine("  lru");
                Console.WriteLine("");
                Console.Write("Selection > ");
                string userInput = Console.ReadLine();
                if (String.IsNullOrEmpty(userInput)) continue;

                switch (userInput.ToLower())
                {
                    case "fifo":
                        FIFOCacheTest();
                        runForever = false;
                        break;

                    case "lru":
                        LRUCacheTest();
                        runForever = false;
                        break;

                    default:
                        continue;
                }
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
}