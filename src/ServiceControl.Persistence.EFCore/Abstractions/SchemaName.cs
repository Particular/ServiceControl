namespace ServiceControl.Persistence.EFCore.Abstractions;

using System.Text.RegularExpressions;

/// <summary>
/// Validation for the configured database schema. A schema name cannot be passed as a parameter,
/// it is interpolated into DDL and into the raw SQL the dialects build, so the allowlist here is
/// what makes that safe.
/// </summary>
public static partial class SchemaName
{
    /// <summary>
    /// PostgreSQL truncates identifiers at 63 bytes and SQL Server allows 128, so the lower limit
    /// applies to both and the same configured name works on either provider.
    /// </summary>
    public const int MaxLength = 63;

    public static string Validate(string schema)
    {
        if (string.IsNullOrWhiteSpace(schema))
        {
            throw new ArgumentException("A database schema name cannot be empty.", nameof(schema));
        }

        if (schema.Length > MaxLength)
        {
            throw new ArgumentException($"Database schema name '{schema}' is longer than the {MaxLength} character limit.", nameof(schema));
        }

        if (!AllowedName().IsMatch(schema))
        {
            throw new ArgumentException($"Database schema name '{schema}' is not valid. Use a letter or an underscore followed by letters, digits or underscores.", nameof(schema));
        }

        return schema;
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex AllowedName();
}
