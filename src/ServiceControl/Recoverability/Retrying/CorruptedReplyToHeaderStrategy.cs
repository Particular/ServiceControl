namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using Microsoft.Extensions.Logging;
    using NServiceBus;

    class CorruptedReplyToHeaderStrategy
    {
        public CorruptedReplyToHeaderStrategy(string localMachineName, ILogger logger)
        {
            this.localMachineName = localMachineName;
            this.logger = logger;
        }

        public void FixCorruptedReplyToHeader(IDictionary<string, string> headers)
        {
            if (!headers.TryGetValue(Headers.ReplyToAddress, out var replyToAddress))
            {
                return;
            }

            if (!headers.TryGetValue(Headers.OriginatingMachine, out var originatingMachine))
            {
                return;
            }

            var split = replyToAddress.Split('@');
            if (split.Length != 2)
            {
                return;
            }

            var queueName = split[0];
            var machineName = split[1];

            if (machineName == localMachineName && machineName != originatingMachine)
            {
                var fixedReplyToAddress = $"{queueName}@{originatingMachine}";
                logger.LogInformation("Detected corrupted ReplyToAddress `{ReplyToAddress}`. Correcting to `{FixedReplyToAddress}`", replyToAddress, fixedReplyToAddress);
                headers["ServiceControl.OldReplyToAddress"] = replyToAddress;
                headers[Headers.ReplyToAddress] = fixedReplyToAddress;
            }
        }

        string localMachineName;
        readonly ILogger logger;
    }
}