namespace ServiceControl.Transport.Tests
{
    using System;
    using NServiceBus.Logging;

    class TestContextAppenderFactory : ILoggerFactory
    {
        public ILog GetLogger(Type type) => GetLogger(type.FullName);

        public ILog GetLogger(string name) => new TestContextAppender();
    }
}