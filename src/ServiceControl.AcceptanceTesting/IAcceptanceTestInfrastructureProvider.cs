namespace ServiceControl.AcceptanceTesting
{
    using System.Net.Http;
    using System.Text.Json;

    public interface IAcceptanceTestInfrastructureProvider
    {
        HttpClient HttpClient { get; }
        JsonSerializerOptions SerializerOptions { get; }
    }
}