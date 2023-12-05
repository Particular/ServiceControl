namespace ServiceControlInstaller.Engine.Validation
{
    using System;

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
    }
}