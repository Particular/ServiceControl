namespace ServiceControl.Transports.SQS
{
    using System;
    using System.Text;

    static class QueueNameHelper
    {
        // copied from https://github.com/Particular/NServiceBus.AmazonSQS/blob/402230de0989ab333124c124cdfdeec14b642263/src/NServiceBus.AmazonSQS/QueueNameHelper.cs
        public static string GetSqsQueueName(string queue, string queueNamePrefix = "")
        {
            // for now, there's no way to deliver queueNamePrefix used on the client side. An empty one is assumed.

            if (string.IsNullOrWhiteSpace(queue))
            {
                throw new ArgumentNullException(nameof(queue));
            }

            var s = queueNamePrefix + queue;

            if (s.Length > 80)
            {
                throw new Exception($"Address {queue} with configured prefix {queueNamePrefix} is longer than 80 characters and therefore cannot be used to create an SQS queue. Use a shorter queue name.");
            }

            var skipCharacters = s.EndsWith(".fifo") ? 5 : 0;
            var queueNameBuilder = new StringBuilder(s);

            // SQS queue names can only have alphanumeric characters, hyphens and underscores.
            // Any other characters will be replaced with a hyphen.
            for (var i = 0; i < queueNameBuilder.Length - skipCharacters; ++i)
            {
                var c = queueNameBuilder[i];
                if (!char.IsLetterOrDigit(c)
                    && c != '-'
                    && c != '_')
                {
                    queueNameBuilder[i] = '-';
                }
            }

            return queueNameBuilder.ToString();
        }
    }
}