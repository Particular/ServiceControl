namespace ServiceControlInstaller.CustomActions
{
    using Engine;
    using Microsoft.Deployment.WindowsInstaller;

    public class MSILogger : ILogging
    {
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

        Session _session;
    }
}