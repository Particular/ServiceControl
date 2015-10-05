namespace ServiceControlInstaller.Engine.Queues
{
    using System;

    public class ServiceControlQueueCreationFailedException : Exception
    {
        public ServiceControlQueueCreationFailedException(string message) : base(message)
        {
        }

        public ServiceControlQueueCreationFailedException(string message, Exception inner) : base(message, inner)
        {
        }

        protected ServiceControlQueueCreationFailedException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context)
        { }
    }
}