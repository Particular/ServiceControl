namespace ServiceControl.Monitoring.AcceptanceTests.TestSupport
{
    using System;
    using System.Collections.Concurrent;
    using System.Reflection;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Logging;
    using NServiceBus.AcceptanceTesting;

    static class ContextLoggerExtensions
    {
        public static ILoggingBuilder AddScenarioContextLogging(this ILoggingBuilder builder)
        {
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, ContextLoggerProvider>());
            return builder;
        }

        class ContextLoggerProvider : ILoggerProvider
        {
            readonly ConcurrentDictionary<string, ILogger> loggers = new ConcurrentDictionary<string, ILogger>();

            public void Dispose() => loggers.Clear();

            public ILogger CreateLogger(string categoryName)
            {
                try
                {
                    return loggers.GetOrAdd(categoryName, name => new ContextLogger(name));
                }
                catch (Exception e)
                {
                    Console.WriteLine($"#### Fail to get logger. Exception: {e}");
                    throw;
                }
            }
        }

        class ContextLogger : ILogger
        {
            readonly string categoryName;

            public ContextLogger(string categoryName)
            {
                this.categoryName = categoryName;
            }

            ScenarioContext GetContext()
            {
                var propertyInfo = typeof(ScenarioContext).GetProperty("Current", BindingFlags.NonPublic | BindingFlags.Static);

                return (ScenarioContext)propertyInfo.GetValue(null);
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception exception, Func<TState, Exception, string> formatter)

            {
                try
                {
                    GetContext().Logs.Enqueue(new ScenarioContext.LogItem
                    {
                        LoggerName = categoryName,
                        Message = $"{state}" + (exception == null ? string.Empty : $"\n{exception}"), //HINT: default Microsoft formatter will ignore the exception
                        Level = ConvertLogLevel(logLevel)
                    });
                }
                catch (Exception e)
                {
                    Console.WriteLine($"#### Fail to log message. Exception: {e}");
                }
            }

            NServiceBus.Logging.LogLevel ConvertLogLevel(LogLevel level)
                => level switch
                {
                    LogLevel.Critical => NServiceBus.Logging.LogLevel.Fatal,
                    LogLevel.Trace => NServiceBus.Logging.LogLevel.Debug,
                    LogLevel.Debug => NServiceBus.Logging.LogLevel.Debug,
                    LogLevel.Information => NServiceBus.Logging.LogLevel.Info,
                    LogLevel.Warning => NServiceBus.Logging.LogLevel.Warn,
                    LogLevel.Error => NServiceBus.Logging.LogLevel.Error,
                    LogLevel.None => NServiceBus.Logging.LogLevel.Debug,
                    _ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
                };

            public bool IsEnabled(LogLevel logLevel)
            {
                try
                {
                    return ConvertLogLevel(logLevel) >= GetContext()?.LogLevel;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"#### Fail to log message. Exception: {e}");
                }

                return false;
            }

            public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

            class NullScope : IDisposable
            {
                public void Dispose() { }

                public static NullScope Instance { get; } = new NullScope();
            }
        }
    }
}