namespace ServiceControl.Persistence.EFCore.Infrastructure;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;

// Single-row-per-key storage for the handful of values that have no other home: they are read and
// written whole, never queried by their contents, so each is stored as a JSON document.
static class SettingsStore
{
    public static async Task<T?> GetSetting<T>(this ServiceControlDbContext dbContext, string key, CancellationToken cancellationToken = default)
    {
        var value = await dbContext.Settings
            .AsNoTracking()
            .Where(setting => setting.Key == key)
            .Select(setting => setting.Value)
            .SingleOrDefaultAsync(cancellationToken);

        return value is null ? default : JsonSerializer.Deserialize<T>(value);
    }

    public static Task StoreSetting<T>(this ServiceControlDbContext dbContext, string key, T value, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(value);

        return dbContext.UpsertAsync(
            [key],
            () => new SettingEntity { Key = key, Value = json },
            setting => setting.Value = json,
            cancellationToken);
    }
}
