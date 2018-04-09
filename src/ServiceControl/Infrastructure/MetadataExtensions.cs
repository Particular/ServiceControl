namespace ServiceControl
{
    using System.Collections.Generic;

    static class MetadataExtensions
    {
        public static T GetOrDefault<T>(this IDictionary<string, object> metadata, string key)
        {
            object foundValue;
            if (metadata.TryGetValue(key, out foundValue))
            {
                return (T)foundValue;
            }
            return default(T);
        }

        public static string GetAsStringOrNull(this IDictionary<string, object> metadata, string key)
        {
            object foundValue;
            if (metadata.TryGetValue(key, out foundValue))
            {
                return foundValue?.ToString();
            }

            return null;
        }
    }
}