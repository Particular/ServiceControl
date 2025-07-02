namespace ServiceControl.Infrastructure.TestLogger
{
    using Microsoft.Extensions.Logging;

    public class TestContextProvider : ILoggerProvider
    {
        readonly LogLevel level;

        public TestContextProvider(LogLevel level)
        {
            this.level = level;
        }

        public ILogger CreateLogger(string categoryName) => new TestContextAppender(categoryName, level);

        public void Dispose()
        {

        }
    }
}
