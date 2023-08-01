
namespace NServiceBus
{
    using System;

    /// <summary>
    /// Represents a delayed message.
    /// </summary>
    public class DelayedMessage
    {
        /// <summary>
        /// Date and time the message is due.
        /// </summary>
        public DateTime Time { get; set; }

        /// <summary>
        /// Native message ID
        /// </summary>
        public string MessageId { get; set; }

        /// <summary>
        /// The body of the message.
        /// </summary>
        public byte[] Body { get; set; }

        /// <summary>
        /// The serialized headers of the message
        /// </summary>
        public byte[] Headers { get; set; }

        /// <summary>
        /// The address of the destination queue
        /// </summary>
        public string Destination { get; set; }

        /// <summary>
        /// The number of attempt already made to forward the message to its destination.
        /// </summary>
        public int NumberOfRetries { get; set; }
    }
}