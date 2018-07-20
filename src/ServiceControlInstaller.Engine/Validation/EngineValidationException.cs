namespace ServiceControlInstaller.Engine.Validation
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    public class EngineValidationException : Exception
    {
        public EngineValidationException()
        {
        }

        public EngineValidationException(string message) : base(message)
        {
        }

        public EngineValidationException(string message, Exception inner) : base(message, inner)
        {
        }

        protected EngineValidationException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
}