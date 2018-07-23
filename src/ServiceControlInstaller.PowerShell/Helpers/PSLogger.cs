namespace ServiceControlInstaller.PowerShell
{
    using System.Management.Automation.Host;
    using Engine;

    public class PSLogger : ILogging
    {
        public PSLogger(PSHost Host)
        {
            host = Host;
        }

        public void Info(string message)
        {
            host.UI.WriteVerboseLine(message);
        }

        public void Warn(string message)
        {
            host.UI.WriteWarningLine(message);
        }

        public void Error(string message)
        {
            host.UI.WriteErrorLine(message);
        }

        PSHost host;
    }
}