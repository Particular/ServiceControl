namespace ServiceControl.Infrastructure
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using NLog.Extensions.Logging;
    using ServiceControl.Infrastructure.TestLogger;

    [Flags]
    public enum Loggers
    {
        None = 0,
        Test = 1 << 0,
        NLog = 1 << 1,
        Seq = 1 << 2,
    }

    public static class LoggerUtil
    {
        public static Loggers ActiveLoggers { private get; set; } = Loggers.None;

        public static void BuildLogger(this ILoggingBuilder loggingBuilder, LogLevel level)
        {
            if ((Loggers.Test & ActiveLoggers) == Loggers.Test)
            {
                loggingBuilder.Services.AddSingleton<ILoggerProvider>(new TestContextProvider(level));
            }
            if ((Loggers.NLog & ActiveLoggers) == Loggers.NLog)
            {
                loggingBuilder.AddNLog();
            }
            if ((Loggers.Seq & ActiveLoggers) == Loggers.Seq)
            {
                loggingBuilder.AddSeq();
            }

            loggingBuilder.SetMinimumLevel(level);
        }

        public static ILogger<T> CreateStaticLogger<T>(LogLevel level = LogLevel.Information)
        {
            var factory = LoggerFactory.Create(configure => configure.BuildLogger(level));
            return factory.CreateLogger<T>();
        }

        public static ILogger CreateStaticLogger(Type type, LogLevel level = LogLevel.Information)
        {
            var factory = LoggerFactory.Create(configure => configure.BuildLogger(level));
            return factory.CreateLogger(type);
        }
    }
}