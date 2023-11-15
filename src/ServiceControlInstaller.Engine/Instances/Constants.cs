namespace ServiceControlInstaller.Engine.Instances
{
    using System.Reflection;
    using NuGet.Versioning;

    public static class Constants
    {
        public const string ServiceControlExe = "ServiceControl.exe";
        public const string ServiceControlAuditExe = "ServiceControl.Audit.exe";
        public const string MonitoringExe = "ServiceControl.Monitoring.exe";

        public static SemanticVersion CurrentVersion { get; }

        static Constants()
        {
            var attributes = Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyInformationalVersionAttribute>();
            SemanticVersion majorMinorPatch = null;

            foreach (var attribute in attributes)
            {
                majorMinorPatch = SemanticVersion.Parse(attribute.InformationalVersion);
            }

            CurrentVersion = majorMinorPatch;
        }
    }
}