namespace ServiceBus.Management.Infrastructure.OWIN
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http.Headers;
    using System.Reflection;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Infrastructure.WebApi;

    // TODO this is only used by the ServiceControlComponentRunner
    class Startup
    {
        public Startup(IServiceProvider serviceProvider, List<Assembly> assemblies)
        {
            this.serviceProvider = serviceProvider;
            this.assemblies = assemblies;
        }

        public void Configuration(IAppBuilder app, Assembly additionalAssembly = null)
        {
            if (additionalAssembly != null)
            {
                assemblies.Add(additionalAssembly);
            }

            app.Map("/api", b =>
            {
                b.UseWebApi(config);
            });
        }

        readonly IServiceProvider serviceProvider;
        readonly List<Assembly> assemblies;
    }
}