namespace ServiceControl.Infrastructure
{
    using System;

    public class UnrecoverableException : Exception
    {
        public UnrecoverableException(string message) : base(message)
        {
        }

        public UnrecoverableException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}