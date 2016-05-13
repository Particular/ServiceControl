namespace ServiceControlInstaller.Engine
{
    public class Logging
    {
        ILogging logger;

        public Logging(ILogging loggingInstance)
        {
            logger = loggingInstance;
        }

        public void Info(string message, params object[] args)
        {
            logger?.Info(string.Format(message, args));
        }

        public void Error(string message, params object[] args)
        {
            logger?.Error(string.Format(message, args));
        }

        public void Warn(string message, params object[] args)
        {
            logger?.Warn(string.Format(message, args));
        }
    }
}
