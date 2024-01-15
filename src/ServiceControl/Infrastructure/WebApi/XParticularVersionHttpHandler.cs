namespace ServiceControl.Infrastructure.WebApi
{
    using System.Reflection;
    using Microsoft.AspNetCore.Mvc.Filters;

    class XParticularVersionHttpHandler : IResultFilter
    {
        static XParticularVersionHttpHandler()
        {
            FileVersion = GetFileVersion();
        }

        static string GetFileVersion()
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

        static readonly string FileVersion;
        public void OnResultExecuting(ResultExecutingContext context)
        {
            context.HttpContext.Response.Headers["X-Particular-Version"] = FileVersion;
        }

        public void OnResultExecuted(ResultExecutedContext context)
        {
            // NOP
        }
    }
}