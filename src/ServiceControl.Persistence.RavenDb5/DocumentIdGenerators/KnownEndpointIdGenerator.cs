using System;

static class KnownEndpointIdGenerator
{
    const string CollectionName = "KnownEndpoint";
    public static string MakeDocumentId(Guid endpointId) => $"{CollectionName}/{endpointId}";
}