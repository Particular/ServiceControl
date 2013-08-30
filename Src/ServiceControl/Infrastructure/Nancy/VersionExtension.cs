namespace ServiceBus.Management.Infrastructure.Nancy
{
    using System.Reflection;
    using global::Nancy;

    public static class VersionExtension
    {
        static VersionExtension()
        {
            FileVersion = GetFileVersion();
        }

        public static void Add(NancyContext ctx)
        {
            ctx.Response
                .WithHeader("X-Particular-Version", FileVersion);
        }

        static string GetFileVersion()
        {
            var customAttributes =
                typeof(VersionExtension).Assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute),
                    false);

            if (customAttributes.Length >= 1)
            {
                var fileVersionAttribute = (AssemblyInformationalVersionAttribute) customAttributes[0];
                return fileVersionAttribute.InformationalVersion;
            }

            return typeof(VersionExtension).Assembly.GetName().Version.ToString(4);
        }

        static readonly string FileVersion;
    }
}