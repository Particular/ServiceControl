namespace ServiceControlInstaller.Engine.Queues
{
    using System;

    public  class ServiceControlQueueCreationTimeoutException : Exception
    {
    
        public ServiceControlQueueCreationTimeoutException(string message) : base(message)
        {
        }

        public ServiceControlQueueCreationTimeoutException(string message, Exception inner) : base(message, inner)
        {
        }

        protected ServiceControlQueueCreationTimeoutException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context)
        { }

    }
}