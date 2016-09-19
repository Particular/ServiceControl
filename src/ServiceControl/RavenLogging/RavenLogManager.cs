namespace ServiceControl.RavenLogging
{
    using System;
    using NLog;
    using Raven.Abstractions.Extensions;
    using Raven.Abstractions.Logging;
    using LogLevel = NLog.LogLevel;
    using LogManager = NLog.LogManager;

    internal class RavenLogManager : ILogManager
    {
        private readonly LogLevel level;

        public RavenLogManager(LogLevel level)
        {
            this.level = level;
        }

        public ILog GetLogger(string name)
        {
            return new RavenLogger(new Lazy<Logger>(() => LogManager.GetLogger(name)), level);
        }

        public IDisposable OpenNestedConext(string message)
        {
            return NestedDiagnosticsContext.Push(message);
        }

        public IDisposable OpenMappedContext(string key, string value)
        {
            MappedDiagnosticsContext.Set(key, value);
            return new DisposableAction(() => MappedDiagnosticsContext.Remove(key));
        }
    }
}