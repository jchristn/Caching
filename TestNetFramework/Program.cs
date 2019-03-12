using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Caching;

namespace TestNetFramework
{
    partial class Program
    {
        static void Main(string[] args)
        {
            bool runForever = true;
            while (runForever)
            {
                Console.WriteLine("-------------------------------------------------------------------------------");
                Console.WriteLine("Select which cache you wish to test");
                Console.WriteLine("  fifo");
                Console.WriteLine("  lru");
                Console.WriteLine("  lrubtree");
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

                    case "lrubtree":
                        LRUCacheBTreeTest();
                        runForever = false;
                        break;

                    default:
                        continue;
                }
            }
        }
    }
}