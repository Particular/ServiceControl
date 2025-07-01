namespace ServiceControl.Infrastructure
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using NLog.Extensions.Logging;
    using OpenTelemetry.Logs;
    using ServiceControl.Infrastructure.TestLogger;

    [Flags]
    public enum Loggers
    {
        None = 0,
        Test = 1 << 0,
        NLog = 1 << 1,
        Seq = 1 << 2,
        Otlp = 1 << 3,
    }

    public static class LoggerUtil
    {
        public static Loggers ActiveLoggers { private get; set; } = Loggers.None;

        public static string SeqAddress { private get; set; }

        public static bool IsLoggingTo(Loggers logger)
        {
            return (logger & ActiveLoggers) == logger;
        }

        public static void BuildServiceControlLogging(this ILoggingBuilder loggingBuilder, LogLevel level)
        {
            if (IsLoggingTo(Loggers.Test))
            {
                loggingBuilder.Services.AddSingleton<ILoggerProvider>(new TestContextProvider(level));
            }
            if (IsLoggingTo(Loggers.NLog))
            {
                loggingBuilder.AddNLog();
            }
            if (IsLoggingTo(Loggers.Seq))
            {
                if (!string.IsNullOrWhiteSpace(SeqAddress))
                {
                    loggingBuilder.AddSeq(SeqAddress);
                }
                else
                {
                    loggingBuilder.AddSeq();
                }
            }
            if (IsLoggingTo(Loggers.Otlp))
            {
                loggingBuilder.AddOpenTelemetry(configure => configure.AddOtlpExporter());
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