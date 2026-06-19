namespace ServiceControl.Infrastructure
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Reflection;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using NLog.Extensions.Logging;
    using OpenTelemetry.Logs;
    using OpenTelemetry.Resources;
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

        // Telemetry resource attached to exported OTLP logs (service.name/service.version/service.instance.id).
        // Set once at process startup via Initialize() — before any logger is created — so both the host pipeline
        // and the static bootstrap loggers (CreateStaticLogger) share a single instance identity. Defaults to
        // CreateDefault() (which still honors OTEL_SERVICE_NAME/OTEL_RESOURCE_ATTRIBUTES) for the rare logger
        // created before Initialize runs.
        static ResourceBuilder serviceResourceBuilder = CreateResourcesBuilder();

        static ResourceBuilder CreateResourcesBuilder()
        {
            var asm = Assembly.GetEntryAssembly() ?? throw new InvalidOperationException("Entry assembly not found");
            var serviceName = asm.GetName().Name ?? throw new InvalidOperationException("Entry assembly name not found");
            var serviceVersion = FileVersionInfo.GetVersionInfo(asm.Location).ProductVersion;

            // CreateDefault() also reads OTEL_SERVICE_NAME/OTEL_RESOURCE_ATTRIBUTES, so operators can still enrich
            // the resource with deployment-specific attributes via those environment variables.
            return ResourceBuilder
                .CreateDefault()
                .AddService(
                    serviceName,
                    serviceVersion: serviceVersion,
                    autoGenerateServiceInstanceId: true
                    );
        }

        public static bool IsLoggingTo(Loggers logger) => (logger & ActiveLoggers) == logger;

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
                loggingBuilder.AddOpenTelemetry(configure =>
                {
                    configure.SetResourceBuilder(serviceResourceBuilder);
                    configure.AddOtlpExporter();
                });
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