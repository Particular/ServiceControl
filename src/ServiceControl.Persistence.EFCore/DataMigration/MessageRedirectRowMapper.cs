namespace ServiceControl.Persistence.EFCore.DataMigration;

using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.MessageRedirects;

/// <summary>
/// Turns a redirect from the source into the row the product's own save would write.
/// </summary>
static class MessageRedirectRowMapper
{
    public static MessageRedirectEntity Map(MessageRedirect redirect) => new()
    {
        FromPhysicalAddress = redirect.FromPhysicalAddress,
        ToPhysicalAddress = redirect.ToPhysicalAddress,
        LastModified = redirect.LastModified
    };
}
