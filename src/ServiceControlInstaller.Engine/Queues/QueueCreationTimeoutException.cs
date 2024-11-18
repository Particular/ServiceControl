namespace ServiceControlInstaller.Engine.Queues
{
    using System;

    public class QueueCreationTimeoutException : Exception
    {
        public QueueCreationTimeoutException(string message) : base(message)
        {
        }

        public QueueCreationTimeoutException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}