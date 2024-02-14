namespace ServiceControl.Infrastructure
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

        static Guid DeterministicGuidBuilder(string input)
        {
            var inputBytes = Encoding.Default.GetBytes(input);

            // use MD5 hash to get a 16-byte hash of the string
            var hashBytes = MD5.HashData(inputBytes);

            // generate a guid from the hash:
            var g = new Guid(hashBytes);

            return g;
        }
    }
}