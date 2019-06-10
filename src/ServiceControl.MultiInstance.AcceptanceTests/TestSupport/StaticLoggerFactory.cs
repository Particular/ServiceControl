namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Logging;

    class StaticLoggerFactory : ILoggerFactory
    {
        public StaticLoggerFactory(ScenarioContext currentContext)
        {
            CurrentContext = currentContext;
        }

        public ILog GetLogger(Type type)
        {
            return GetLogger(type.FullName);
        }

        public ILog GetLogger(string name)
        {
            return new StaticContextAppender();
        }

        public static ScenarioContext CurrentContext;
    }
}