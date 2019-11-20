namespace ServiceControl.AcceptanceTesting
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