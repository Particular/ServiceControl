namespace ServiceControlInstaller.Engine.Queues
{
    using System;

    public  class QueueCreationTimeoutException : Exception
    {
    
        public QueueCreationTimeoutException(string message) : base(message)
        {
        }

        public QueueCreationTimeoutException(string message, Exception inner) : base(message, inner)
        {
        }

        protected QueueCreationTimeoutException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context)
        { }

    }
}