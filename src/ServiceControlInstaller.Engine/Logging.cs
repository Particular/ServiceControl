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
            if (logger == null)
                return;
            logger.Info(string.Format(message, args));
        }

        public void Error(string message, params object[] args)
        {
            if (logger == null)
                return;
            logger.Error(string.Format(message, args));
        }

        public void Warn(string message, params object[] args)
        {
            if (logger == null)
                return;
            logger.Warn(string.Format(message, args));
        }
    }
}
