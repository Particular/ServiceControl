namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.Reflection;

    public static class Constants
    {
        public const string ServiceControlExe = "ServiceControl.exe";
        public const string ServiceControlAuditExe = "ServiceControl.Audit.exe";
        public const string MonitoringExe = "ServiceControl.Monitoring.exe";

        public static Version CurrentVersion { get; }

        static Constants()
        {
            var attributes = Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>();
            Version majorMinorPatch = null;

            foreach (var attribute in attributes)
            {
                if (attribute.Key == "MajorMinorPatch")
                {
                    majorMinorPatch = new Version(attribute.Value);
                }
            }

            CurrentVersion = majorMinorPatch;
        }
    }
}