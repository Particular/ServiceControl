namespace ServiceControl.Infrastructure
{
    using System.Collections.Generic;

    static class CollectionExtensions
    {
        public static void AddRange<T>(this IList<T> list, params T[] items)
        {
            foreach (var item in items)
            {
                list.Add(item);
            }
        }
    }
}