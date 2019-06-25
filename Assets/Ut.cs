using System;
using System.Collections.Generic;

namespace TheUltracube
{
    static class Ut
    {
        public static int IndexOf<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        {
            var i = 0;
            foreach (var obj in source)
            {
                if (predicate(obj))
                    return i;
                i++;
            }
            return -1;
        }
    }
}
