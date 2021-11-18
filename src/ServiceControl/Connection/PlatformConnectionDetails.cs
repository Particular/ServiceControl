namespace ServiceControl.Connection
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    public class PlatformConnectionDetails
    {
        readonly ConcurrentDictionary<string, object> values = new ConcurrentDictionary<string, object>();

        public void Add(string key, object value)
        {
            if (values.TryAdd(key, value))
            {
                return;
            }

            // Add a numeric suffix to duplicated keys
            var suffix = 0;
            do
            {
                suffix++;
            } while (!values.TryAdd($"{key}{suffix}", value));
        }

        public IDictionary<string, object> ToDictionary() => values;

        public ConcurrentBag<string> Errors { get; } = new ConcurrentBag<string>();
    }
}