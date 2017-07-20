namespace ServiceControlInstaller.Engine.Configuration
{
    using System;

    public class SettingInfo
    {
        public string Name { get; set; }
        public Version SupportedFrom { get; set; }
        public Version RemovedFrom { get;  set; }
    }
}