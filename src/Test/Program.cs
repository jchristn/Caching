using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Caching;

namespace Test
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
    }
}