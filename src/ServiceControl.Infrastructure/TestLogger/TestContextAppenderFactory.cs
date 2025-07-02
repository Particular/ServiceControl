namespace ServiceControl.Infrastructure.TestLogger
{
    using Microsoft.Extensions.Logging;

    public class TestContextAppenderFactory(LogLevel logLevel) : ILoggerFactory
    {
        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName) => new TestContextAppender(categoryName, logLevel);

        public void Dispose()
        {
        }
    }
}