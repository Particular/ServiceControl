namespace ServiceControl.Infrastructure
{
    using System;
    using Microsoft.Extensions.Logging;
    using NLog.Extensions.Logging;

    public static class LoggerUtil
    {
        public static void BuildLogger(this ILoggingBuilder loggingBuilder, LogLevel level)
        {
            loggingBuilder.AddNLog();
            loggingBuilder.AddSeq();
            loggingBuilder.SetMinimumLevel(level);
        }

        public static ILogger<T> CreateStaticLogger<T>(LogLevel level = LogLevel.Information)
        {
            var factory = LoggerFactory.Create(configure => configure.BuildLogger(level));
            var logger = factory.CreateLogger<T>();
            logger.LogError("JTD: This is a test log message.");
            return logger;
        }

        public static ILogger CreateStaticLogger(Type type, LogLevel level = LogLevel.Information)
        {
            var factory = LoggerFactory.Create(configure => configure.BuildLogger(level));
            return factory.CreateLogger(type);
        }
    }
}