namespace Particular.HealthMonitoring.Uptime
{
    using System;
    using System.Security.Cryptography;
    using System.Text;

    static class DeterministicGuid
    {
        public static Guid MakeId(string data)
        {
            return DeterministicGuidBuilder(data);
        }

        public static Guid MakeId(string data1, string data2)
        {
            return DeterministicGuidBuilder($"{data1}{data2}");
        }

        public static Guid MakeId(string data1, string data2, string data3)
        {
            return DeterministicGuidBuilder($"{data1}{data2}{data3}");
        }

        private static Guid DeterministicGuidBuilder(string input)
        {
            Guid g;

            // use MD5 hash to get a 16-byte hash of the string
            using (var provider = new MD5CryptoServiceProvider())
            {
                var inputBytes = Encoding.Default.GetBytes(input);
                var hashBytes = provider.ComputeHash(inputBytes);
                // generate a guid from the hash:
                g = new Guid(hashBytes);
            }
            
            return g;
        }
    }
}
