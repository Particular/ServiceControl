namespace ServiceBus.Management.AcceptanceTests
{
    using System.Net.Http;
    using Newtonsoft.Json;

    public interface IAcceptanceTestInfrastructureProvider
    {
        HttpClient HttpClient { get; }
        JsonSerializerSettings SerializerSettings { get; }
        string Port { get; }
    }
}