namespace ServiceControl.Transport.Tests;

using System;
using NServiceBus.Logging;

class TestContextAppenderFactory : ILoggerFactory
{
    public ILog GetLogger(Type type)
    {
        return GetLogger(type.FullName);
    }

    public ILog GetLogger(string name)
    {
        return new TestContextAppender();
    }
}