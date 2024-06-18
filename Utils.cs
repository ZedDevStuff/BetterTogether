using System;
using System.Collections.Generic;
using System.Text;

namespace BetterTogetherCore
{
    internal static class Utils
    {
        public static bool FastStartsWith(this string str, string value)
        {
            if (str.Length < value.Length) return false;
            for (int i = 0; i < value.Length; i++)
            {
                if (str[i] != value[i]) return false;
            }
            return true;
        }
    }
}