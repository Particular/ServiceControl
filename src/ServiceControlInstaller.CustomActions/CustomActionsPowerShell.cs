namespace ServiceControlInstaller.CustomActions
{
    using System;
    using System.Linq;
    using System.Runtime.InteropServices;
    using Microsoft.Deployment.WindowsInstaller;

    public class CustomActionsPowerShell
    {
        const string PSModulePath = "PSMODULEPATH";

        [DllImport("User32.DLL")]
        static extern int SendMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

        [CustomAction]
        public static ActionResult AddToPSModuleEnvironmentVar(Session session)
        {
            //Advanced Installer doesn't notify of environment changes on system environment variables
            var appDir = session["MODULEDIR"];
            if (appDir.EndsWith(@"\"))
            {
                appDir = appDir.Remove(appDir.Length - 1);
            }

            var environmentVariable = Environment.GetEnvironmentVariable(PSModulePath, EnvironmentVariableTarget.Machine);
            if (environmentVariable != null)
            {
                var parts = environmentVariable.Split(";".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
                if (!parts.Any(p => p.Equals(appDir, StringComparison.OrdinalIgnoreCase)))
                {
                    parts.Add(appDir);
                    var newValue = string.Join(";", parts);
                    Environment.SetEnvironmentVariable(PSModulePath, newValue, EnvironmentVariableTarget.Machine);
                    Log(session, "Updated PowerShell module path at " + appDir);
                }
            }
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult RemoveFromPSModuleEnvironmentVar(Session session)
        {
            //Advanced Installer doesn't notify of environment changes on system environment variables
            var appDir = session["MODULEDIR"];

            var environmentVariable = Environment.GetEnvironmentVariable(PSModulePath, EnvironmentVariableTarget.Machine);
            if (environmentVariable != null)
            {
                var parts = environmentVariable.Split(";".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
                if (parts.Any(p => p.Equals(appDir, StringComparison.OrdinalIgnoreCase)))
                {
                    var newParts = parts.Where(p => !p.Equals(appDir, StringComparison.OrdinalIgnoreCase)).ToList();
                    var newValue = string.Join(";", newParts);
                    Environment.SetEnvironmentVariable(PSModulePath, newValue, EnvironmentVariableTarget.Machine);
                    Log(session, "Removed PowerShell module path.");
                }
            }
            return ActionResult.Success;
        }

        static void Log(Session session, string message, params object[] args)
        {
            LogAction(session, string.Format(message, args));
        }

        public static Action<Session, string> LogAction = (s, m) => s.Log(m);

        public static Func<Session, string, string> GetAction = (s, key) => s[key];

        public static Action<Session, string, string> SetAction = (s, key, value) => s[key] = value;
    }

    public static class SessionExtensions
    {
        public static string Get(this Session session, string key)
        {
            return CustomActionsPowerShell.GetAction(session, key);
        }

        public static void Set(this Session session, string key, string value)
        {
            CustomActionsPowerShell.SetAction(session, key, value);
        }
    }
}