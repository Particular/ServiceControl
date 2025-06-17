namespace ServiceControl.Infrastructure.TestLogger
{
    using Microsoft.Extensions.Logging;

    public class TestContextProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new TestContextAppender(categoryName);

        public void Dispose()
        {

        }
    }
}
