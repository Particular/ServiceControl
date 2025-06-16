namespace ServiceControl.Transport.Tests
{
    using Microsoft.Extensions.Logging;

    class TestContextAppenderFactory : ILoggerFactory
    {
        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName) => new TestContextAppender(categoryName);

        public void Dispose()
        {
        }
    }
}