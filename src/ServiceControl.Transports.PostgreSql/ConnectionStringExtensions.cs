namespace ServiceControl.Transports.PostgreSql;

using System.Data.Common;

public static class ConnectionStringExtensions
{
    public static string RemoveCustomConnectionStringParts(this string connectionString, out string schema, out string subscriptionTable) =>
        connectionString
            .RemoveCustomConnectionStringPart(SubscriptionsTableName, out subscriptionTable)
            .RemoveCustomConnectionStringPart(QueueSchemaName, out schema);

    static string RemoveCustomConnectionStringPart(this string connectionString, string partName, out string partValue)
    {
        var builder = new DbConnectionStringBuilder
        {
            ConnectionString = connectionString
        };

        if (builder.TryGetValue(partName, out var customPartValue))
        {
            builder.Remove(partName);
        }

        partValue = (string)customPartValue;

        if (partValue != null && connectionString.Contains(PostgreSqlNameHelper.Quote(partValue)))
        {
            partValue = PostgreSqlNameHelper.Quote(partValue);
        }

        return builder.ConnectionString;
    }

    const string QueueSchemaName = "Queue Schema";
    const string SubscriptionsTableName = "Subscriptions Table";
}