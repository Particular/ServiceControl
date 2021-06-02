namespace Particular.ServiceControl
{
    using System;
    using Microsoft.Extensions.Hosting;

    static class HostBuilderExtensions
    {
        public static IHostBuilder If(this IHostBuilder hostBuilder, bool value, Func<IHostBuilder, IHostBuilder> conditionalUse)
        {
            if (value)
            {
                conditionalUse(hostBuilder);
            }

            return hostBuilder;
        }
    }
}