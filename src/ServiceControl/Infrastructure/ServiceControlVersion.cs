namespace ServiceControl.Infrastructure
{
    using ServiceControl.Infrastructure.WebApi;
    using System.Reflection;

    public class ServiceControlVersion
    {
        public static string GetFileVersion()
        {
            var customAttributes = typeof(XParticularVersionHttpHandler).Assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false);

            if (customAttributes.Length >= 1)
            {
                var fileVersionAttribute = (AssemblyInformationalVersionAttribute)customAttributes[0];
                var informationalVersion = fileVersionAttribute.InformationalVersion;
                return informationalVersion.Split('+')[0];
            }

            return typeof(XParticularVersionHttpHandler).Assembly.GetName().Version.ToString(4);
        }
    }
}
