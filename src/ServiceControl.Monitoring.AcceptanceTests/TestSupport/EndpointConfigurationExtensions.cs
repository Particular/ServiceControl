namespace ServiceControl.Monitoring.AcceptanceTests.TestSupport;

using System.IO;
using AcceptanceTesting;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.Configuration.AdvancedExtensibility;

static class EndpointConfigurationExtensions
{
    public static void CustomizeServiceControlMonitoringEndpointTesting(this EndpointConfiguration configuration, ScenarioContext context)
    {
        configuration.GetSettings().Set("SC.ScenarioContext", context);
        configuration.GetSettings().Set(context);

        configuration.RegisterComponents(r =>
        {
            r.AddSingleton(context.GetType(), context);
            r.AddSingleton(typeof(ScenarioContext), context);
        });

        configuration.Pipeline.Register<TraceIncomingBehavior.Registration>();
        configuration.Pipeline.Register<TraceOutgoingBehavior.Registration>();
        configuration.Pipeline.Register(new StampDispatchBehavior(context), "Stamps outgoing messages with session ID");
        configuration.Pipeline.Register(new DiscardMessagesBehavior(context), "Discards messages based on session ID");

        var assemblyScanner = configuration.AssemblyScanner();
        assemblyScanner.ExcludeAssemblies(Path.GetFileName(typeof(ServiceControlComponentRunner).Assembly.Location));
    }
}