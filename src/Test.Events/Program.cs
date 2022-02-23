using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Caching;

namespace Test.Events
{
    partial class Program
    {
        static int _Capacity = 2048;
        static int _EvictCount = 512;
        static int _LoadCount = 256;
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

        private static void Replaced(object sender, DataEventArgs<string, byte[]> e)
        {
            Console.WriteLine("*** Cache entry " + e.Key + " replaced");
        }

        private static void Removed(object sender, DataEventArgs<string, byte[]> e)
        {
            Console.WriteLine("*** Cache entry " + e.Key + " removed");
        }

        private static void Prepopulated(object sender, DataEventArgs<string, byte[]> e)
        {
            Console.WriteLine("*** Cache entry " + e.Key + " prepopulated from persistent storage");
        }

        private static void Evicted(object sender, List<string> e)
        {
            Console.WriteLine("*** Eviction event involving " + e.Count + " entries");
            foreach (string curr in e) Console.WriteLine("    | " + curr);
        }

        private static void Disposed(object sender, EventArgs e)
        {
            Console.WriteLine("*** Disposed");
        }

        private static void Cleared(object sender, EventArgs e)
        {
            Console.WriteLine("*** Cache cleared");
        }

        private static void Added(object sender, DataEventArgs<string, byte[]> e)
        {
            Console.WriteLine("*** Cache entry " + e.Key + " added");
        }
    }
}