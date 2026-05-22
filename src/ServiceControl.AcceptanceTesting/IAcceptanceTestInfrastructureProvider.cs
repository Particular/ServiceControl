namespace ServiceControl.AcceptanceTesting
{
    using System;
    using System.Net.Http;
    using System.Text.Json;

    public interface IAcceptanceTestInfrastructureProvider
    {
        HttpClient HttpClient { get; }
        JsonSerializerOptions SerializerOptions { get; }

        /// <summary>
        /// The DI container of the running ServiceControl host.
        /// Exposed so tests can resolve internal services (e.g. <c>IEnumerable&lt;EndpointDataSource&gt;</c>
        /// for the endpoint-completeness test) without coupling to a specific host type.
        /// </summary>
        IServiceProvider Services { get; }
    }
}