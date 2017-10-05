namespace ServiceControl.LearningTransport
{
    using System;
    using System.IO;
    using System.Text;
    using NServiceBus;

    class MessageDispatcher
    {
        public MessageDispatcher(LearningTransportUnitOfWork unitOfWork)
        {
            maxMessageSizeKb = int.MaxValue / 1024;
            this.unitOfWork = unitOfWork;
        }

        public void Dispatch(TransportMessage message, PathCalculator.MessageBasePaths basePaths, string replyToAddress, bool enlist)
        {
            PathChecker.ThrowForBadPath(basePaths.Header, "message destination");
            PathChecker.ThrowForBadPath(basePaths.Body, "body destination");

            Directory.CreateDirectory(basePaths.Header);
            Directory.CreateDirectory(basePaths.Body);

            message.Headers[Headers.ReplyToAddress] = replyToAddress;

            var nativeMessageId = Guid.NewGuid().ToString();

            var bodyPath = Path.Combine(basePaths.Body, nativeMessageId) + PathCalculator.BodyFileSuffix;

            FileOps.WriteBytes(bodyPath, message.Body, true);

            if (message.TimeToBeReceived < TimeSpan.MaxValue)
            {
                message.Headers[LearningTransportHeaders.TimeToBeReceived] = message.TimeToBeReceived.ToString();
            }

            var messagePath = Path.Combine(basePaths.Header, nativeMessageId) + ".metadata.txt";

            var headerPayload = HeaderSerializer.Serialize(message.Headers);
            var headerSize = Encoding.UTF8.GetByteCount(headerPayload);

            var bodySize = message.Body?.Length ?? 0;

            if (headerSize + bodySize > maxMessageSizeKb * 1024)
            {
                throw new Exception($"The total size of the '{message.Headers[Headers.EnclosedMessageTypes]}' message body ({bodySize} bytes) plus headers ({headerSize} bytes) is larger than {maxMessageSizeKb} KB and will not be supported on some production transports. Consider using the NServiceBus DataBus or the claim check pattern to avoid messages with a large payload. Use 'EndpointConfiguration.UseTransport<LearningTransport>().NoPayloadSizeRestriction()' to disable this check and proceed with the current message size.");
            }

            if (enlist && unitOfWork.HasActiveTransaction)
            {
                unitOfWork.Transaction.Enlist(messagePath, headerPayload);
            }
            else
            {
                // atomic avoids the file being locked when the receiver tries to process it
                FileOps.WriteTextAtomic(messagePath, headerPayload);
            }
        }

        readonly int maxMessageSizeKb;
        LearningTransportUnitOfWork unitOfWork;
    }
}
