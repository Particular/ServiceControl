namespace TestHelper;

using System.Text.RegularExpressions;

public static partial class MigrationScript
{
    /// <summary>
    /// Replaces the EF Core product version in each migrations history row of a migration script with <c>&lt;ProductVersion&gt;</c>.
    /// The version changes with every EF Core update, but the schema stays the same.
    /// </summary>
    public static string WithoutProductVersion(string script) => HistoryRowVersion().Replace(script, "${row}<ProductVersion>'");

    [GeneratedRegex(@"(?<row>VALUES \(N?'\d{14}_\w+', N?')[^']+'")]
    private static partial Regex HistoryRowVersion();
}
