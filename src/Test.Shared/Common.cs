using System;
using System.Collections.Generic;

namespace Test.Shared
{
    public static class Common
    {
        private static readonly List<string> _Values = new()
        {
            "Alpha",
            "Bravo",
            "Charlie",
            "Delta",
            "Echo",
            "Foxtrot",
            "Golf",
            "Hotel",
            "India",
            "Juliett",
            "Kilo",
            "Lima",
            "Mike",
            "November",
            "Oscar",
            "Papa",
            "Quebec",
            "Romeo",
            "Sierra",
            "Tango",
            "Uniform",
            "Victor",
            "Whiskey",
            "X-ray",
            "Yankee",
            "Zulu"
        };

        public static (int, string) GetCodeWord(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            int sum = 0;
            foreach (char c in key) sum += (int)c;

            (int, string) ret = new()
            {
                Item1 = sum % 26
            };
            ret.Item2 = _Values[ret.Item1];
            return ret;
        }

        public static byte[] InitByteArray(int count, byte val)
        {
            byte[] ret = new byte[count];
            for (int i = 0; i < ret.Length; i++)
            {
                ret[i] = val;
            }
            return ret;
        }

        public static string TotalTimeFrom(DateTime startTime)
        {
            DateTime endTime = DateTime.Now;
            TimeSpan totalTime = (endTime - startTime);

            if (totalTime.TotalDays > 1)
            {
                return DecimalToString(totalTime.TotalDays) + "days";
            }
            else if (totalTime.TotalHours > 1)
            {
                return DecimalToString(totalTime.TotalHours) + "hrs";
            }
            else if (totalTime.TotalMinutes > 1)
            {
                return DecimalToString(totalTime.TotalMinutes) + "mins";
            }
            else if (totalTime.TotalSeconds > 1)
            {
                return DecimalToString(totalTime.TotalSeconds) + "sec";
            }
            else if (totalTime.TotalMilliseconds > 0)
            {
                return DecimalToString(totalTime.TotalMilliseconds) + "ms";
            }
            else
            {
                return "<unknown>";
            }
        }

        public static double TotalMsFrom(DateTime startTime)
        {
            try
            {
                DateTime endTime = DateTime.Now;
                TimeSpan totalTime = (endTime - startTime);
                return totalTime.TotalMilliseconds;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        public static string DecimalToString(object obj)
        {
            if (obj == null) return null;
            if (obj is string) if (String.IsNullOrEmpty(obj.ToString())) return null;
            string ret = string.Format("{0:N2}", obj);
            ret = ret.Replace(",", "");
            return ret;
        }

        public static void PrintException(Exception e)
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