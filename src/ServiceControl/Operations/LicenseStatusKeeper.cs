namespace ServiceControl.Operations
{
    using System.Collections.Concurrent;

    public class LicenseStatusKeeper
    {
        ConcurrentDictionary<string, string> cache = new ConcurrentDictionary<string, string>();

        public void Set(string key, string value)
        {
            cache.AddOrUpdate(key, value, (s, s1) => value);
        }

        public string Get(string key)
        {
            return cache.TryGetValue(key, out var value) ? value : "unknown";
        }
    }
}