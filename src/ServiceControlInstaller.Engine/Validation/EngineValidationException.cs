using System;

namespace ServiceControlInstaller.Engine.Validation
{
    public class EngineValidationException : Exception
    {
        public EngineValidationException(string message) : base(message)
        {
        }
    }
}
