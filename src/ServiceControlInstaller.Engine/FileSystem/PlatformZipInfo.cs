namespace ServiceControlInstaller.Engine.FileSystem
{
    using System;
    using System.Reflection;

    public class PlatformZipInfo
    {
        public PlatformZipInfo(string mainEntrypoint, string name, string zipResourceName)
        {
            Name = name;
            MainEntrypoint = mainEntrypoint;
            ResourceName = zipResourceName;
        }

        public string MainEntrypoint { get; }

        public string Name { get; }

        public string ResourceName { get; }

        public void ValidateZip()
        {
            if (string.IsNullOrEmpty(ResourceName))
            {
                throw new Exception("Empty zip file resource name");
            }

            _ = Assembly.GetExecutingAssembly().GetManifestResourceInfo(ResourceName)
                ?? throw new Exception($"Missing zip file resource '{ResourceName}'");
        }
    }
}