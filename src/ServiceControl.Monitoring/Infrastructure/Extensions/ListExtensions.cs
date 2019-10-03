namespace ServiceControl.Monitoring.Infrastructure.Extensions
{
    using System;
    using System.Collections.Generic;

    public static class ListExtensions
    {
        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (var obj in source)
            {
                action(obj);
            }
        }

        public static void AddRange<T>(this IList<T> list, IEnumerable<T> items)
        {
            foreach (var obj in items)
            {
                list.Add(obj);
            }
        }
    }
}