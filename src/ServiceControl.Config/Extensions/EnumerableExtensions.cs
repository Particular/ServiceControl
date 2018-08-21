namespace ServiceControl.Config
{
    using System;
    using System.Collections.Generic;

    static class EnumerableExtensions
    {
        public static void Apply<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (var item in source)
            {
                action(item);
            }
        }

        public static int IndexOf<T>(this IEnumerable<T> list, T item, IEqualityComparer<T> comparer = null)
        {
            var comp = comparer ?? EqualityComparer<T>.Default;

            var i = 0;
            foreach (var x in list)
            {
                if (comp.Equals(x, item))
                {
                    return i;
                }

                i++;
            }

            return -1;
        }
    }
}