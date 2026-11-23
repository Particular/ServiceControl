namespace ServiceControl.Persistence.DataMigration;

using System;
using System.Data.Common;
using System.Linq;
using ServiceControl.Configuration;

/// <summary>
/// One value the migration will use, as step 1 of --start-data-migration prints it. Origin is the full key of
/// the setting it came from, or <c>default</c> when no setting has a value.
/// </summary>
public sealed record MigrationSettingValue(string Label, string Value, string Origin)
{
    static readonly string[] SecretWords = ["password", "pwd", "psw", "secret", "key", "token", "signature"];

    /// <summary>
    /// The full key when the setting has a value, or <c>default</c> when it has none.
    /// </summary>
    public static string OriginOf(SettingsRootNamespace settingsRoot, string key) =>
        SettingsReader.Read<string>(settingsRoot, key) is null ? "default" : $"{settingsRoot}/{key}";

    /// <summary>
    /// Replaces the value of every part of a connection string whose key names a password, secret, key, token or signature with
    /// <c>*****</c>. A string that does not parse is replaced whole, because nothing shows where its secret ends.
    /// </summary>
    public static string MaskSecrets(string connectionString)
    {
        var builder = new DbConnectionStringBuilder();

        try
        {
            builder.ConnectionString = connectionString;
        }
        catch (ArgumentException)
        {
            return "***** (not a valid connection string)";
        }

        var secretKeys = builder.Keys.Cast<string>().Where(key => SecretWords.Any(word => key.Contains(word, StringComparison.OrdinalIgnoreCase))).ToList();

        if (secretKeys.Count == 0)
        {
            return connectionString;
        }

        foreach (var key in secretKeys)
        {
            builder[key] = "*****";
        }

        return builder.ConnectionString;
    }
}
