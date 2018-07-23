// Equal to NServiceBus.NLog but required for setting up Scenario.GetLoggerFactory due to private methods on the package's implementation.
namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using NLog;
    using NServiceBus.Logging;

    class NLogLoggerFactory : ILoggerFactory
    {
        public ILog GetLogger(Type type)
        {
            return new NLogLogger(NLog.LogManager.GetLogger(type.FullName));
        }

        public ILog GetLogger(string name)
        {
            return new NLogLogger(NLog.LogManager.GetLogger(name));
        }

        class NLogLogger : ILog
        {
            Logger logger;

            public NLogLogger(Logger logger)
            {
                this.logger = logger;
            }

            public void Debug(string message)
            {
                logger.Debug(message);
            }

            public void Debug(string message, Exception exception)
            {
                logger.Debug(exception, message);
            }

            public void DebugFormat(string format, params object[] args)
            {
                logger.Debug(format, args);
            }

            public void Info(string message)
            {
                logger.Info(message);
            }

            public void Info(string message, Exception exception)
            {
                logger.Info(exception, message);
            }

            public void InfoFormat(string format, params object[] args)
            {
                logger.Info(format, args);
            }

            public void Warn(string message)
            {
                logger.Warn(message);
            }

            public void Warn(string message, Exception exception)
            {
                logger.Warn(exception, message);
            }

            public void WarnFormat(string format, params object[] args)
            {
                logger.Warn(format, args);
            }

            public void Error(string message)
            {
                logger.Error(message);
            }

            public void Error(string message, Exception exception)
            {
                logger.Error(exception, message);
            }

            public void ErrorFormat(string format, params object[] args)
            {
                logger.Error(format, args);
            }

            public void Fatal(string message)
            {
                logger.Fatal(message);
            }

            public void Fatal(string message, Exception exception)
            {
                logger.Fatal(exception, message);
            }

            public void FatalFormat(string format, params object[] args)
            {
                logger.Fatal(format, args);
            }

            public bool IsDebugEnabled => logger.IsDebugEnabled;
            public bool IsInfoEnabled => logger.IsInfoEnabled;
            public bool IsWarnEnabled => logger.IsWarnEnabled;
            public bool IsErrorEnabled => logger.IsErrorEnabled;
            public bool IsFatalEnabled => logger.IsFatalEnabled;
        }
    }
}