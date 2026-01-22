namespace ServiceControl.Audit.Persistence.Sql.Core.Infrastructure;

/// <summary>
/// Generates sequential GUIDs for database primary keys to minimize page fragmentation
/// and improve insert performance while maintaining security benefits of GUIDs.
/// </summary>
/// <remarks>
/// This implementation creates time-ordered GUIDs similar to .NET 9's Guid.CreateVersion7()
/// but compatible with .NET 8. The GUIDs are ordered by timestamp to reduce B-tree page splits
/// in clustered indexes, which significantly improves insert performance compared to random GUIDs.
///
/// Benefits:
/// - Database agnostic (works with SQL Server, PostgreSQL, MySQL, SQLite)
/// - Sequential ordering reduces page fragmentation
/// - Better insert performance than random GUIDs
/// - Can easily migrate to Guid.CreateVersion7() when upgrading to .NET 9+
/// - No external dependencies
///
/// Security:
/// - Still cryptographically secure (uses Guid.NewGuid() as base)
/// - Not guessable (unlike sequential integers)
/// - Safe to expose in APIs
/// </remarks>
public static class SequentialGuidGenerator
{
    /// <summary>
    /// Generate a sequential GUID with timestamp-based ordering for optimal database performance.
    /// </summary>
    /// <returns>A new GUID with sequential characteristics.</returns>
    public static Guid NewSequentialGuid()
    {
        var guidBytes = Guid.NewGuid().ToByteArray();
        var now = DateTime.UtcNow;

        // Get timestamp in milliseconds since Unix epoch (similar to Version 7 GUIDs)
        var timestamp = (long)(now - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        var timestampBytes = BitConverter.GetBytes(timestamp);

        // Reverse if little-endian to get big-endian byte order for proper sorting
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(timestampBytes);
        }

        // Replace last 6 bytes with timestamp for sequential ordering
        // This placement works well with SQL Server's GUID comparison semantics
        Array.Copy(timestampBytes, 2, guidBytes, 10, 6);

        return new Guid(guidBytes);
    }
}
