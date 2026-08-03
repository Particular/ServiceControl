namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

static class ColumnLengths
{
    // Indexed and short-by-nature values get a length so that SQL Server can index them,
    // nvarchar(max) columns cannot be index key columns.
    public const int ShortTextLength = 450;

    // Group ids are deterministic Guid strings, shared by the group rows and their comments.
    public const int GroupIdLength = 64;

    // The subscriptions key spans two columns and SQL Server caps a clustered index key at 900 bytes,
    // so both have to stay well under ShortTextLength. Matches NServiceBus.Persistence.Sql.
    public const int SubscriptionKeyLength = 200;
}
