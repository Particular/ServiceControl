namespace ServiceControlInstaller.Engine.Queues
{
    using System;
    using System.Runtime.Serialization;

    public class ServiceControlQueueCreationTimeoutException : Exception
    {
        public ServiceControlQueueCreationTimeoutException(string message) : base(message)
        {
        }

        public ServiceControlQueueCreationTimeoutException(string message, Exception inner) : base(message, inner)
        {
        }

        protected ServiceControlQueueCreationTimeoutException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
}