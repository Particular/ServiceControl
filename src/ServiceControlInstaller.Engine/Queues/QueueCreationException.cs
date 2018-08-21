namespace ServiceControlInstaller.Engine.Queues
{
    using System;
    using System.Runtime.Serialization;

    public class QueueCreationFailedException : Exception
    {
        public QueueCreationFailedException(string message) : base(message)
        {
        }

        public QueueCreationFailedException(string message, Exception inner) : base(message, inner)
        {
        }

        protected QueueCreationFailedException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
}