using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace BetterTogetherCore
{
    internal static class Utils
    {
        /// <summary>
        /// Fast implementation of StartsWith
        /// </summary>
        /// <param name="str"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool FastStartsWith(this string str, string value)
        {
            if (str.Length < value.Length) return false;
            for (int i = 0; i < value.Length; i++)
            {
                if (str[i] != value[i]) return false;
            }
            return true;
        }
        public static Regex guidRegex = new Regex(@"^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}");
    }
}