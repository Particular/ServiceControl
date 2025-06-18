namespace ServiceControl.Infrastructure.TestLogger
{
    using Microsoft.Extensions.Logging;

    public class TestContextAppenderFactory : ILoggerFactory
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