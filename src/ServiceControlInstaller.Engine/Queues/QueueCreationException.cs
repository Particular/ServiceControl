namespace ServiceControlInstaller.Engine.Queues
{
    using System;

    public class QueueCreationFailedException : Exception
    {
        public QueueCreationFailedException(string message) : base(message)
        {
        }

        public QueueCreationFailedException(string message, Exception inner) : base(message, inner)
        {
        }

        protected QueueCreationFailedException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context)
        { }
    }
}