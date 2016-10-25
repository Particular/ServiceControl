namespace ServiceControl.Infrastructure
{
    using System;
    using System.Collections.Concurrent;
    using System.Security.Cryptography;
    using System.Text;

    public static class DeterministicGuid
    {
        static ConcurrentDictionary<string, Guid> cacheData = new ConcurrentDictionary<string, Guid>();

        public static Guid MakeId(string data, bool cache = true)
        {
            return DeterministicGuidBuilder(data, cache);
        }

        public static Guid MakeId(string data1, string data2, bool cache = true)
        {
            return DeterministicGuidBuilder($"{data1}{data2}", cache);
        }

        public static Guid MakeId(string data1, string data2, string data3, bool cache = true)
        {
            return DeterministicGuidBuilder($"{data1}{data2}{data3}", cache);
        }

        private static Guid DeterministicGuidBuilder(string input, bool cache)
        {
            Guid g;

            if (cache)
            {
                if (cacheData.TryGetValue(input, out g))
                {
                    return g;
                }
            }

            // use MD5 hash to get a 16-byte hash of the string
            using (var provider = new MD5CryptoServiceProvider())
            {
                var inputBytes = Encoding.Default.GetBytes(input);
                var hashBytes = provider.ComputeHash(inputBytes);
                // generate a guid from the hash:
                g = new Guid(hashBytes);
            }

            if (cache)
            {
                cacheData.TryAdd(input, g);
            }

            return g;
        }
    }
}
