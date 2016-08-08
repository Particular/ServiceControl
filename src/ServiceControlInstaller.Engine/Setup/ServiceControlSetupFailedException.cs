namespace ServiceControlInstaller.Engine.Setup
{
    using System;

    public class ServiceControlSetupFailedException : Exception
    {
        public ServiceControlSetupFailedException(string message) : base(message)
        {
        }

        public ServiceControlSetupFailedException(string message, Exception inner) : base(message, inner)
        {
        }

        protected ServiceControlSetupFailedException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context)
        { }
    }
}