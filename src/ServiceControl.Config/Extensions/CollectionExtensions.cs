using System.Collections.Generic;

namespace ServiceControl.Config
{

    static class CollectionExtensions
    {
        public static bool Any<T>(this ICollection<T> source)
        {
            return source.Count > 0;
        }
    }
}