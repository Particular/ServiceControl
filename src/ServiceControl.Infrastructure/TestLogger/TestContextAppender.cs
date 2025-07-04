﻿namespace ServiceControl.Infrastructure.TestLogger
{
    using System;
    using Microsoft.Extensions.Logging;
    using NUnit.Framework;

    class TestContextAppender(string categoryName, LogLevel level) : ILogger
    {
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                TestContext.Out.WriteLine($"{categoryName}: {formatter(state, exception)}");
            }
        }
        public bool IsEnabled(LogLevel logLevel) => logLevel >= level;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => Disposable.Instance;

        class Disposable : IDisposable
        {
            public static Disposable Instance = new();

            public void Dispose()
            {
            }
        }
    }
}