namespace ServiceControl.RavenLogging
{
    using System;
    using NLog;
    using Raven.Abstractions.Logging;
    using LogLevel = NLog.LogLevel;

    internal class RavenLogger : ILog
    {
        private static readonly object[] EmptyArray = new object[0];
        private readonly Lazy<Logger> lazyLogger;
        private readonly int levelInt;

        public RavenLogger(Lazy<Logger> lazyLogger, LogLevel level)
        {
            this.lazyLogger = lazyLogger;
            levelInt = level.Ordinal;
        }

        public void Log(Raven.Abstractions.Logging.LogLevel logLevel, Func<string> messageFunc)
        {
            if (!ShouldLog(logLevel))
            {
                return;
            }

            switch (logLevel)
            {
                case Raven.Abstractions.Logging.LogLevel.Debug:
                    if (lazyLogger.Value.IsDebugEnabled)
                    {
                        lazyLogger.Value.Debug(messageFunc());
                    }
                    break;
                case Raven.Abstractions.Logging.LogLevel.Info:
                    if (lazyLogger.Value.IsInfoEnabled)
                    {
                        lazyLogger.Value.Info(messageFunc());
                    }
                    break;
                case Raven.Abstractions.Logging.LogLevel.Warn:
                    if (lazyLogger.Value.IsWarnEnabled)
                    {
                        lazyLogger.Value.Warn(messageFunc());
                    }
                    break;
                case Raven.Abstractions.Logging.LogLevel.Error:
                    if (lazyLogger.Value.IsErrorEnabled)
                    {
                        lazyLogger.Value.Error(messageFunc());
                    }
                    break;
                case Raven.Abstractions.Logging.LogLevel.Fatal:
                    if (lazyLogger.Value.IsFatalEnabled)
                    {
                        lazyLogger.Value.Fatal(messageFunc());
                    }
                    break;
                default:
                    if (lazyLogger.Value.IsTraceEnabled)
                    {
                        lazyLogger.Value.Trace(messageFunc());
                    }
                    break;
            }
        }

        public void Log<TException>(Raven.Abstractions.Logging.LogLevel logLevel, Func<string> messageFunc, TException exception) where TException : Exception
        {
            if (!ShouldLog(logLevel))
            {
                return;
            }

            switch (logLevel)
            {
                case Raven.Abstractions.Logging.LogLevel.Debug:
                    if (lazyLogger.Value.IsDebugEnabled)
                    {
                        lazyLogger.Value.Debug(exception, messageFunc(), EmptyArray);
                    }
                    break;
                case Raven.Abstractions.Logging.LogLevel.Info:
                    if (lazyLogger.Value.IsInfoEnabled)
                    {
                        lazyLogger.Value.Info(exception, messageFunc(), EmptyArray);
                    }
                    break;
                case Raven.Abstractions.Logging.LogLevel.Warn:
                    if (lazyLogger.Value.IsWarnEnabled)
                    {
                        lazyLogger.Value.Warn(exception, messageFunc(), EmptyArray);
                    }
                    break;
                case Raven.Abstractions.Logging.LogLevel.Error:
                    if (lazyLogger.Value.IsErrorEnabled)
                    {
                        lazyLogger.Value.Error(exception, messageFunc(), EmptyArray);
                    }
                    break;
                case Raven.Abstractions.Logging.LogLevel.Fatal:
                    if (lazyLogger.Value.IsFatalEnabled)
                    {
                        lazyLogger.Value.Fatal(exception, messageFunc(), EmptyArray);
                    }
                    break;
                default:
                    if (lazyLogger.Value.IsTraceEnabled)
                    {
                        lazyLogger.Value.Trace(exception, messageFunc(), EmptyArray);
                    }
                    break;
            }
        }

        public bool ShouldLog(Raven.Abstractions.Logging.LogLevel logLevel)
        {
            return (int) logLevel >= levelInt;
        }

        public bool IsDebugEnabled => ShouldLog(Raven.Abstractions.Logging.LogLevel.Debug);

        public bool IsWarnEnabled => ShouldLog(Raven.Abstractions.Logging.LogLevel.Warn);
    }
}