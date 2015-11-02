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
        public static byte[] init_byte_array(int count, byte val)
        {
            byte[] ret = new byte[count];
            for (int i = 0; i < ret.Length; i++)
            {
                ret[i] = val;
            }
            return ret;
        }

        private static string total_time_from(DateTime start_time)
        {
            DateTime end_time = DateTime.Now;
            TimeSpan total_time = (end_time - start_time);

            if (total_time.TotalDays > 1)
            {
                return decimal_tostring(total_time.TotalDays) + "days";
            }
            else if (total_time.TotalHours > 1)
            {
                return decimal_tostring(total_time.TotalHours) + "hrs";
            }
            else if (total_time.TotalMinutes > 1)
            {
                return decimal_tostring(total_time.TotalMinutes) + "mins";
            }
            else if (total_time.TotalSeconds > 1)
            {
                return decimal_tostring(total_time.TotalSeconds) + "sec";
            }
            else if (total_time.TotalMilliseconds > 0)
            {
                return decimal_tostring(total_time.TotalMilliseconds) + "ms";
            }
            else
            {
                return "<unknown>";
            }
        }

        private static double total_ms_from(DateTime start_time)
        {
            try
            {
                DateTime end_time = DateTime.Now;
                TimeSpan total_time = (end_time - start_time);
                return total_time.TotalMilliseconds;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        private static string decimal_tostring(object obj)
        {
            if (obj == null) return null;
            if (obj is string) if (String.IsNullOrEmpty(obj.ToString())) return null;
            string ret = string.Format("{0:N2}", obj);
            ret = ret.Replace(",", "");
            return ret;
        }

        static void print_exception(Exception e)
        {
            Console.WriteLine("================================================================================");
            Console.WriteLine("Exception Type: " + e.GetType().ToString());
            Console.WriteLine("Exception Data: " + e.Data);
            Console.WriteLine("Inner Exception: " + e.InnerException);
            Console.WriteLine("Exception Message: " + e.Message);
            Console.WriteLine("Exception Source: " + e.Source);
            Console.WriteLine("Exception StackTrace: " + e.StackTrace);
            Console.WriteLine("================================================================================");
        }
    }
}
