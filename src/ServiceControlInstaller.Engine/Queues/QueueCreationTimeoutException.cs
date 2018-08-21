namespace ServiceControlInstaller.Engine.Queues
{
    using System;
    using System.Runtime.Serialization;

    public class QueueCreationTimeoutException : Exception
    {
        public QueueCreationTimeoutException(string message) : base(message)
        {
        }

        public QueueCreationTimeoutException(string message, Exception inner) : base(message, inner)
        {
        }

        protected QueueCreationTimeoutException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
}