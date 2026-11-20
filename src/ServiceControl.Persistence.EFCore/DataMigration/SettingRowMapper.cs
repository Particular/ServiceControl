namespace ServiceControl.Persistence.EFCore.DataMigration;

using System.Text.Json;
using ServiceControl.Persistence.EFCore.Entities;

/// <summary>
/// Builds the Settings row for a value, storing it under the given key as the JSON text that
/// <c>SettingsStore.StoreSetting</c> writes. <typeparamref name="T" /> is the type the product stores under that key.
/// </summary>
static class SettingRowMapper
{
    public static SettingEntity Map<T>(string key, T value) => new() { Key = key, Value = JsonSerializer.Serialize(value) };
}
