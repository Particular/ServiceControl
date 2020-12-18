namespace ServiceControlInstaller.CustomActions
{
    using Engine;
    using Microsoft.Deployment.WindowsInstaller;

    public class MSILogger : ILogging
    {
        public MSILogger(Session session)
        {
            this.session = session;
        }

        public void Info(string message)
        {
            session.Log(message);
        }

        public void Warn(string message)
        {
            session.Log("WARN: {0}", message);
        }

        public void Error(string message)
        {
            session.Log("ERROR: {0}", message);
        }

        Session session;
    }
}