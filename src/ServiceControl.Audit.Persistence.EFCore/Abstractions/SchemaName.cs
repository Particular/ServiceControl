namespace ServiceControl.Audit.Persistence.EFCore.Abstractions;

using System.Text.RegularExpressions;

// The name is interpolated into DDL and raw SQL, so this allowlist is what keeps that safe.
public static partial class SchemaName
{
    // PostgreSQL truncates identifiers at 63 bytes.
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
