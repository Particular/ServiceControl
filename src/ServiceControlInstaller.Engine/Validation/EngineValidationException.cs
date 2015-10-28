using System;

namespace ServiceControlInstaller.Engine.Validation
{
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
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context)
        { }
    }
}