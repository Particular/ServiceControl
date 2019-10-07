namespace ServiceControl.Monitoring
{
    using System.Reflection;

    class VersionInfo
    {
        static VersionInfo()
        {
            FileVersion = GetFileVersion();
        }

        static string GetFileVersion()
        {
            var customAttributes = typeof(VersionInfo).Assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute),
                false);

            if (customAttributes.Length >= 1)
            {
                var fileVersionAttribute = (AssemblyInformationalVersionAttribute)customAttributes[0];
                var informationalVersion = fileVersionAttribute.InformationalVersion;
                return informationalVersion.Split('+')[0];
            }

            return typeof(VersionInfo).Assembly.GetName().Version.ToString(4);
        }

        internal static readonly string FileVersion;
    }
}