namespace ServiceControl.Infrastructure
{
    using System;
    using System.Collections.Concurrent;
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

        public static void ConfigureLogging(this ILoggingBuilder loggingBuilder, LogLevel level)
        {
            loggingBuilder.SetMinimumLevel(level);

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
        }

        static readonly ConcurrentDictionary<LogLevel, ILoggerFactory> _factories = new();

        static ILoggerFactory GetOrCreateLoggerFactory(LogLevel level)
        {
            if (!_factories.TryGetValue(level, out var factory))
            {
                factory = LoggerFactory.Create(configure => configure.ConfigureLogging(level));
                _factories[level] = factory;
            }

            return factory;
        }

        public static ILogger<T> CreateStaticLogger<T>(LogLevel level = LogLevel.Information)
        {
            var factory = GetOrCreateLoggerFactory(level);
            return factory.CreateLogger<T>();
        }

        public static ILogger CreateStaticLogger(Type type, LogLevel level = LogLevel.Information)
        {
            var factory = GetOrCreateLoggerFactory(level);
            return factory.CreateLogger(type);
        }

        public static void DisposeLoggerFactories()
        {
            foreach (var factory in _factories.Values)
            {
                factory.Dispose();
            }

            _factories.Clear();
        }
    }
}