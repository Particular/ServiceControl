namespace ServiceControlInstaller.Engine.Configuration
{
    using NuGet.Versioning;

    public class SettingInfo
    {
        public string Name { get; set; }
        public SemanticVersion SupportedFrom { get; set; }
        public SemanticVersion RemovedFrom { get; set; }
    }
}