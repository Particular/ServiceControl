namespace ServiceControl.Transports.SqlServer
{
    using System;
    using System.Data.Common;

    static class ConnectionStringExtensions
    {
        public static string RemoveCustomConnectionStringParts(this string connectionString, out string schema, out string subscriptionTable)
        {
            return connectionString
                .RemoveCustomConnectionStringPart(queueSchemaName, out schema)
                .RemoveCustomConnectionStringPart(subscriptionsTableName, out subscriptionTable);
        }

        // Extracts the optional, ServiceControl-specific 'QueueLengthQueryDelayInterval' (milliseconds) from
        // the connection string and removes it so it is never handed to SqlConnection (which would reject the
        // unknown keyword). Mirrors the existing convention used by the Azure Service Bus transport.
        // This is the BASE interval used while any monitored queue has messages.
        public static string RemoveQueueLengthQueryDelayInterval(this string connectionString, out TimeSpan? interval) =>
            connectionString.RemoveIntervalMilliseconds(queueLengthQueryDelayInterval, out interval);

        // Extracts the optional 'QueueLengthQueryMaxDelayInterval' (milliseconds) — the upper bound the adaptive
        // back-off ramps to while every monitored queue is empty. When omitted a default ceiling applies; set it
        // equal to the base interval to disable back-off.
        public static string RemoveQueueLengthQueryMaxDelayInterval(this string connectionString, out TimeSpan? interval) =>
            connectionString.RemoveIntervalMilliseconds(queueLengthQueryMaxDelayInterval, out interval);

        static string RemoveIntervalMilliseconds(this string connectionString, string key, out TimeSpan? interval)
        {
            interval = null;

            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

            if (builder.TryGetValue(key, out var value))
            {
                if (!int.TryParse(value.ToString(), out var milliseconds) || milliseconds <= 0)
                {
                    throw new Exception($"Can't parse '{value}' as a valid {key} (expected a positive integer number of milliseconds).");
                }

                interval = TimeSpan.FromMilliseconds(milliseconds);
                builder.Remove(key);
            }

            return builder.ConnectionString;
        }

        public static string RemoveCustomConnectionStringPart(this string connectionString, string partName, out string schema)
        {
            var builder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            if (builder.TryGetValue(partName, out var customSchema))
            {
                builder.Remove(partName);
            }

            schema = (string)customSchema;

            return builder.ConnectionString;
        }

        const string queueSchemaName = "Queue Schema";
        const string subscriptionsTableName = "Subscriptions Table";
        const string queueLengthQueryDelayInterval = "QueueLengthQueryDelayInterval";
        const string queueLengthQueryMaxDelayInterval = "QueueLengthQueryMaxDelayInterval";
    }
}