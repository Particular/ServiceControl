namespace ServiceControl.Monitoring
{
    //using System;
    //using System.ServiceProcess;
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
            //var runAsWindowsService = !Environment.UserInteractive && !settings.Portable;

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
            return host.RunConsoleAsync();

            //if (runAsWindowsService)
            //{
            //    host.RunAsService();

            //    using (var service = new Host { Settings = settings, ServiceName = settings.ServiceName })
            //    {
            //        //HINT: this calls-back to Windows Service Control Manager (SCM) and hangs
            //        //      until service reports it has stopped.
            //        //      SCM takes over and calls OnStart and OnStop on the service instance. 
            //        ServiceBase.Run(service);
            //    }
            //}
            //else
            //{
            //}

            //return Task.CompletedTask;
        }

        //async Task RunAsConsoleApp(Settings settings)
        //{
        //    using (var service = new Host
        //    {
        //        Settings = settings,
        //        ServiceName = settings.ServiceName
        //    })
        //    {
        //        var tcs = new TaskCompletionSource<bool>();

        //        Action done = () =>
        //        {
        //            service.OnStopping = () => { };
        //            tcs.SetResult(true);
        //        };

        //        service.OnStopping = done;

        //        OnConsoleCancel.Run(done);

        //        service.Start();

        //        Console.WriteLine("Press Ctrl+C to exit");

        //        await tcs.Task.ConfigureAwait(false);
        //    }
        //}

        //class OnConsoleCancel
        //{
        //    OnConsoleCancel(Action action)
        //    {
        //        this.action = action;
        //    }

        //    public static void Run(Action action)
        //    {
        //        var onCancelAction = new OnConsoleCancel(action);
        //        Console.CancelKeyPress += onCancelAction.ConsoleOnCancelKeyPress;
        //    }

        //    void ConsoleOnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        //    {
        //        action();
        //        e.Cancel = true;
        //        Console.CancelKeyPress -= ConsoleOnCancelKeyPress;
        //    }

        //    Action action;
        //}
    }
}