namespace ServiceControlInstaller.Engine.Setup
{
    using System;

    public class ServiceControlSetupTimeoutException : Exception
    {

        public ServiceControlSetupTimeoutException(string message) : base(message)
        {
        }

        public ServiceControlSetupTimeoutException(string message, Exception inner) : base(message, inner)
        {
        }

        protected ServiceControlSetupTimeoutException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context)
        { }

    }
}