// ASB doesn't work with the ScenarioContext.Current property which uses the logical CallContext
// as the OnMessage callback loses the previous CallContext.
// To avoid exceptions when accessing the logger, a custom logger with a fixed ScenarioContext needs to be used.
// This requires all tests to run sequentially as the logger is configured statically (LogManager).
namespace ServiceControl.Monitoring.SmokeTests.LegacyAzureServiceBus
{
    using System;
    using System.Diagnostics;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Logging;
    using NUnit.Framework;

    public partial class NServiceBusAcceptanceTest
    {
        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            Scenario.GetLoggerFactory = ctx => new StaticLoggerFactory(ctx);
        }
    }

    class StaticLoggerFactory : ILoggerFactory
    {
        public static ScenarioContext CurrentContext;

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
    }

    class StaticContextAppender : ILog
    {
        public bool IsDebugEnabled => StaticLoggerFactory.CurrentContext.LogLevel <= LogLevel.Debug;
        public bool IsInfoEnabled => StaticLoggerFactory.CurrentContext.LogLevel <= LogLevel.Info;
        public bool IsWarnEnabled => StaticLoggerFactory.CurrentContext.LogLevel <= LogLevel.Warn;
        public bool IsErrorEnabled => StaticLoggerFactory.CurrentContext.LogLevel <= LogLevel.Error;
        public bool IsFatalEnabled => StaticLoggerFactory.CurrentContext.LogLevel <= LogLevel.Fatal;


        public void Debug(string message)
        {
            Log(message, LogLevel.Debug);
        }

        public void Debug(string message, Exception exception)
        {
            var fullMessage = $"{message} {exception}";
            Log(fullMessage, LogLevel.Debug);
        }

        public void DebugFormat(string format, params object[] args)
        {
            var fullMessage = string.Format(format, args);
            Log(fullMessage, LogLevel.Debug);
        }

        public void Info(string message)
        {
            Log(message, LogLevel.Info);
        }


        public void Info(string message, Exception exception)
        {
            var fullMessage = $"{message} {exception}";
            Log(fullMessage, LogLevel.Info);
        }

        public void InfoFormat(string format, params object[] args)
        {
            var fullMessage = string.Format(format, args);
            Log(fullMessage, LogLevel.Info);
        }

        public void Warn(string message)
        {
            Log(message, LogLevel.Warn);
        }

        public void Warn(string message, Exception exception)
        {
            var fullMessage = $"{message} {exception}";
            Log(fullMessage, LogLevel.Warn);
        }

        public void WarnFormat(string format, params object[] args)
        {
            var fullMessage = string.Format(format, args);
            Log(fullMessage, LogLevel.Warn);
        }

        public void Error(string message)
        {
            Log(message, LogLevel.Error);
        }

        public void Error(string message, Exception exception)
        {
            var fullMessage = $"{message} {exception}";
            Log(fullMessage, LogLevel.Error);
        }

        public void ErrorFormat(string format, params object[] args)
        {
            var fullMessage = string.Format(format, args);
            Log(fullMessage, LogLevel.Error);
        }

        public void Fatal(string message)
        {
            Log(message, LogLevel.Fatal);
        }

        public void Fatal(string message, Exception exception)
        {
            var fullMessage = $"{message} {exception}";
            Log(fullMessage, LogLevel.Fatal);
        }

        public void FatalFormat(string format, params object[] args)
        {
            var fullMessage = string.Format(format, args);
            Log(fullMessage, LogLevel.Fatal);
        }

        void Log(string message, LogLevel messageSeverity)
        {
            if (StaticLoggerFactory.CurrentContext.LogLevel > messageSeverity)
            {
                return;
            }

            Trace.WriteLine(message);
            StaticLoggerFactory.CurrentContext.Logs.Enqueue(new ScenarioContext.LogItem
            {
                Level = messageSeverity,
                Message = message
            });
        }
    }
}