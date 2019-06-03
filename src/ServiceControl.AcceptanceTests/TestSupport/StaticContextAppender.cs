namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Diagnostics;
    using System.Reflection;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Logging;

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
                Endpoint = (string)typeof(ScenarioContext).GetProperty("CurrentEndpoint", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(StaticLoggerFactory.CurrentContext),
                Level = messageSeverity,
                Message = message
            });
        }
    }
}