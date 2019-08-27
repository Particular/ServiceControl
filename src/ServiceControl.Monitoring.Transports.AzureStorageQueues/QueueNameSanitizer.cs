namespace ServiceControl.Transports.AzureStorageQueues
{
    using System;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;

    // The name sanitizer has been copied from ASQ as it's its internal part and cannot be accessed otherwise
    // https://github.com/Particular/NServiceBus.AzureStorageQueues/blob/b68358a6e39f04f0d9ecdaa73c6c435731861dd5/src/AcceptanceTests/BackwardsCompatibleQueueNameSanitizerForTests.cs
    public class QueueNameSanitizer
    {
        public static string Sanitize(string queueName)
        {
            var queueNameInLowerCase = queueName.ToLowerInvariant();
            return ShortenQueueNameIfNecessary(queueNameInLowerCase, SanitizeQueueName(queueNameInLowerCase));
        }

        static string ShortenQueueNameIfNecessary(string address, string queueName)
        {
            if (queueName.Length <= 63)
            {
                return queueName;
            }

            var shortenedName = ShortenWithMd5(address);
            queueName = $"{queueName.Substring(0, 63 - shortenedName.Length - 1).Trim('-')}-{shortenedName}";
            return queueName;
        }

        static string SanitizeQueueName(string queueName)
        {
            // this can lead to multiple - occurrences in a row
            var sanitized = invalidCharacters.Replace(queueName, "-");
            sanitized = multipleDashes.Replace(sanitized, "-");
            return sanitized;
        }

        static Regex invalidCharacters = new Regex(@"[^a-zA-Z0-9\-]", RegexOptions.Compiled);
        static Regex multipleDashes = new Regex(@"\-+", RegexOptions.Compiled);

        static string ShortenWithMd5(string queue)
        {
            //use MD5 hash to get a 16-byte hash of the string
            using (var provider = MD5.Create())
            {
                var inputBytes = Encoding.Default.GetBytes(queue);
                var hashBytes = provider.ComputeHash(inputBytes);
                //generate a guid from the hash:
                return new Guid(hashBytes).ToString();
            }
        }
    }
}