namespace ServiceControl.Transports
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class BatchMessage
    {
        public BatchMessage(string messageId, Dictionary<string, string> headers, byte[] body)
        {
            MessageId = messageId;
            Headers = headers;
            Body = body;
        }

        public string MessageId { get; }
        public Dictionary<string, string> Headers { get; }
        public byte[] Body { get; }
    }

    public interface IConsumeBatches
    {
        void Start(Func<IReadOnlyCollection<BatchMessage>, Task> consume, string inputQueue, string connectionString);

        Task Stop();
    }
}