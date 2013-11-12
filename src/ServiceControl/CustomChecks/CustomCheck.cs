namespace ServiceControl.CustomChecks
{
    using System;
    using System.Security.Cryptography;
    using System.Text;
    using ServiceBus.Management.MessageAuditing;

    class CustomCheck
    {
        public Guid Id { get; set; }
        public string CustomCheckId { get; set; }
        public string Category { get; set; }
        public Status Status { get; set; }
        public DateTime ReportedAt { get; set; }
        public string FailureReason { get; set; }
        public EndpointDetails OriginatingEndpoint { get; set; }

        public static Guid MakeId(params string[] data)
        {
            return DeterministicGuidBuilder(String.Concat(data));
        } 

        static Guid DeterministicGuidBuilder(string input)
        {
            // use MD5 hash to get a 16-byte hash of the string
            using (var provider = new MD5CryptoServiceProvider())
            {
                var inputBytes = Encoding.Default.GetBytes(input);
                var hashBytes = provider.ComputeHash(inputBytes);
                // generate a guid from the hash:
                return new Guid(hashBytes);
            }
        }
    }
}