namespace ServiceBus.Management.AcceptanceTests
{
    using System.Net.Http;
    using Newtonsoft.Json;
    using ServiceControl.Monitoring;
    using ServiceControl.Monitoring.Infrastructure;

    interface IAcceptanceTestInfrastructureProvider
    {
        HttpClient HttpClient { get; }

        JsonSerializerSettings SerializerSettings { get; }

        Settings Settings { get; }

        OwinHttpMessageHandler Handler { get; }
        
        BusInstance Bus { get; }
    }
}