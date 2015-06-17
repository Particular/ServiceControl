

namespace DeploymentZipTask
{
    using Microsoft.Build.Framework;

    static class LoggingExtensions
    {
        public static void LogWarning(this ITask task, string message, params object[] args)
        {
            task.BuildEngine.LogWarningEvent(new BuildWarningEventArgs(string.Empty, string.Empty, null, 0, 0, 0, 0, string.Format(message,args), string.Empty, "DeploymentZipTask"));
        }

        public static void LogInfo(this ITask task, string message, params object[] args)
        {
            task.BuildEngine.LogMessageEvent(new BuildMessageEventArgs(string.Format(message,args), string.Empty, "DeploymentZipTask", MessageImportance.Normal));
        }

        public static void LogError(this ITask task, string message, string file = null, params object[] args)
        {
            task.BuildEngine.LogErrorEvent(new BuildErrorEventArgs(string.Format(message, args), string.Empty, file, 0, 0, 0, 0, message, string.Empty, "DeploymentZipTask"));
        }
    }
}
