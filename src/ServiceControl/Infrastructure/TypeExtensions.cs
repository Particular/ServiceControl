namespace ServiceControl
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public static class TypeExtensions
    {
        public static IEnumerable<Type> Implementing<T>(this IEnumerable<Type> types)
        {
            return from type in types
                where typeof(T).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface
                select type;
        }
    }
}