namespace ServiceControl.Persistence.EFCore.EntityConfigurations;

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

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

    // The unacknowledged retry key is this column plus the RetryType int, and ShortTextLength would
    // put it over SQL Server's 900 byte limit. 800 + 4 fits, with room for the queue addresses that
    // ByQueueAddress retries use as their request id.
    public const int RetryRequestIdLength = 400;

    // Applies to both the licensing endpoint name and the normalized name computed from it, which
    // therefore has to hold anything the name can. The key is this column plus the ThroughputSource
    // int, and the throughput key adds a date on top, so ShortTextLength would exceed SQL Server's
    // 900 byte index key limit.
    public const int LicensingEndpointNameLength = 300;

    // Cutting an indexed value short could make two different values look up as equal, so a longer value keeps as much of its start as fits, followed by # and the SHA-256 of the whole value. The result is never longer than ShortTextLength, so fitting it again returns it unchanged.
    [return: NotNullIfNotNull(nameof(value))]
    public static string? FitToIndex(string? value)
    {
        if (value is null || value.Length <= ShortTextLength)
        {
            return value;
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        var prefixLength = ShortTextLength - hash.Length - 1;

        if (char.IsHighSurrogate(value[prefixLength - 1]))
        {
            prefixLength--;
        }

        return $"{value[..prefixLength]}#{hash}";
    }
}
