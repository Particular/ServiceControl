namespace ServiceControlInstaller.Engine.FileSystem
{
    using System;
    using System.Reflection;

    public static class PlatformZipInfoExtensions
    {
        public static void ValidateZip(this PlatformZipInfo zipInfo)
        {
            if (string.IsNullOrEmpty(zipInfo.ResourceName))
            {
                throw new Exception("Empty zip file resource name");
            }

            var resourceInfo = Assembly.GetExecutingAssembly().GetManifestResourceInfo(zipInfo.ResourceName);

            if (resourceInfo is null)
            {
                throw new Exception($"Missing zip file resource '{zipInfo.ResourceName}'");
            }
        }
    }
}