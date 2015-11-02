using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Caching;

namespace CachingTest
{
    partial class Program
    {
        static void Main(string[] args)
        {
            bool run_forever = true;
            while (run_forever)
            {
                Console.WriteLine("-------------------------------------------------------------------------------");
                Console.WriteLine("Select which cache you wish to test");
                Console.WriteLine("  fifo");
                Console.WriteLine("  lru");
                Console.WriteLine("");
                Console.Write("Selection > ");
                string user_input = Console.ReadLine();
                if (String.IsNullOrEmpty(user_input)) continue;

                switch (user_input.ToLower())
                {
                    case "fifo":
                        FIFOCacheTest();
                        run_forever = false;
                        break;

                    case "lru":
                        LRUCacheTest();
                        run_forever = false;
                        break;

                    default:
                        continue;
                }
            }
        }
    }
}