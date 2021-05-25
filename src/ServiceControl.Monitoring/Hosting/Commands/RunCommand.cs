namespace ServiceControl.Monitoring
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Autofac;
    using Autofac.Extensions.DependencyInjection;
    using Autofac.Features.ResolveAnything;
    using Infrastructure.OWIN;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using NLog.Extensions.Logging;
    using NServiceBus;
    using QueueLength;
    using ServiceBus.Management.Infrastructure.OWIN;
    using Transports;

    class RunCommand : AbstractCommand
    {
        public override Task Execute(Settings settings)
        {
            //RunAsWindowsService can't be a property on Settings class because it
            //would be exposed as app.config configurable option and break ATT approvals
            var runAsWindowsService = !Environment.UserInteractive && !settings.Portable;

            var transportCustomization = settings.LoadTransportCustomization();
            var buildQueueLengthProvider = Bootstrapper.QueueLengthProviderBuilder(settings.ConnectionString, transportCustomization);

            var host = new HostBuilder();
            host
                .UseServiceProviderFactory(
                    new AutofacServiceProviderFactory(containerBuilder =>
                    {
                        containerBuilder.RegisterModule<ApplicationModule>();
                        containerBuilder.RegisterSource(new AnyConcreteTypeNotAlreadyRegisteredSource(type => type.Assembly == typeof(Bootstrapper).Assembly && type.GetInterfaces().Any() == false));
                        containerBuilder.RegisterInstance(settings);
                        containerBuilder.Register(c => buildQueueLengthProvider(c.Resolve<QueueLengthStore>())).As<IProvideQueueLength>().SingleInstance();

                        containerBuilder.RegisterModule<ApiControllerModule>();

                        IContainer container = null;
                        containerBuilder.RegisterBuildCallback(c => container = c);
                        containerBuilder.Register(cc => new Startup(container));
                    }))
                .ConfigureServices(services =>
                {
                    services.AddHostedService<WebApiHostedService>();
                })
                .ConfigureLogging(builder =>
                {
                    builder.ClearProviders();
                    //HINT: configuration used by NLog comes from MonitorLog.cs
                    builder.AddNLog();
                })
                .UseNServiceBus(builder =>
                {
                    var configuration = new EndpointConfiguration(settings.ServiceName);

                    var bootstrapper = new Bootstrapper(ctx => { },
                        settings,
                        configuration);

                    return configuration;
                });

            if (runAsWindowsService)
            {
                host.UseWindowsService();
            }
            else
            {
                host.UseConsoleLifetime();
            }

            return host.Build().RunAsync();
        }
    }
}