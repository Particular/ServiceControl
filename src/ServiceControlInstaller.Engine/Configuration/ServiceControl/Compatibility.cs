namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    using NuGet.Versioning;

    // For Removing and Adding Setting use SettingsList.cs
    public static class Compatibility
    {
        public static CompatibilityInfo ForwardingQueuesAreOptional = new CompatibilityInfo { SupportedFrom = new SemanticVersion(1, 29, 0) };
        public static CompatibilityInfo RemoteInstancesDoNotNeedQueueAddress = new CompatibilityInfo { SupportedFrom = new SemanticVersion(4, 0, 0) };

        public class CompatibilityInfo
        {
            public SemanticVersion SupportedFrom { get; set; }
            public SemanticVersion RemovedFrom { get; set; }
        }
    }
}