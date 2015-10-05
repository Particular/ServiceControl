namespace ServiceControlInstaller.CustomActions
{
    using Microsoft.Deployment.WindowsInstaller;
    using ServiceControlInstaller.Engine;

    public class MSILogger : ILogging
    {
        Session _session;

        public MSILogger(Session session)
        {
            _session = session;
        }

        public void Info(string message)
        {
            _session.Log(message);
        }

        public void Warn(string message)
        {
            _session.Log("WARN: {0}", message);
        }

        public void Error(string message)
        {
            _session.Log("ERROR: {0}", message);
        }
    }
}
