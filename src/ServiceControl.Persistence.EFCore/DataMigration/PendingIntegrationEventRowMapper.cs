namespace ServiceControl.Persistence.EFCore.DataMigration;

using Implementation;
using ServiceControl.ExternalIntegrations;
using ServiceControl.Persistence.EFCore.Entities;

static class PendingIntegrationEventRowMapper
{
    /// <summary>
    /// Builds the row for one queued request, with its context stored the way the product stores it. Throws when the request has no context, so a caller must skip such a request before it calls this.
    /// </summary>
    public static ExternalIntegrationDispatchRequestEntity Map(ExternalIntegrationDispatchRequest request)
    {
        // The product's own serializer, so the dispatcher reads a copied row exactly as it reads one it stored.
        var (typeName, json) = DispatchContextSerializer.Serialize(request.DispatchContext);

        return new ExternalIntegrationDispatchRequestEntity { DispatchContextTypeName = typeName, DispatchContextJson = json };
    }
}
