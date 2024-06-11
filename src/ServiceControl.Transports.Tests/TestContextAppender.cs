namespace ServiceControl.Transport.Tests
{
    using System;
    using NServiceBus.Logging;
    using NUnit.Framework;

    class TestContextAppender : ILog
    {
        public bool IsDebugEnabled => false;
        public bool IsInfoEnabled => false;
        public bool IsWarnEnabled => true;
        public bool IsErrorEnabled => true;
        public bool IsFatalEnabled => true;

        public void Debug(string message) => Log(message, LogLevel.Debug);

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

        public void Info(string message) => Log(message, LogLevel.Info);

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

        public void Warn(string message) => Log(message, LogLevel.Warn);

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

        public void Error(string message) => Log(message, LogLevel.Error);

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

        public void Fatal(string message) => Log(message, LogLevel.Fatal);

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

        static void Log(string message, LogLevel _) => TestContext.WriteLine(message);
    }
}