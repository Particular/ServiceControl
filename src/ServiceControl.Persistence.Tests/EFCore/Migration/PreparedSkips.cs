namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.DataMigration;
using ServiceControl.Persistence.EFCore.DbContexts;

static class PreparedSkips
{
    /// <summary>
    /// Prepares a batch through one writer without writing anything, so a test can read the detail line the target logs for each skip.
    /// </summary>
    public static async Task<IReadOnlyList<string>> DetailsFrom(IServiceProvider services, IMigrationCategoryWriter writer, MigrationBatch batch)
    {
        using var scope = services.CreateScope();
        var prepared = await writer.Prepare(scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>(), batch);

        return [.. prepared.Skips.Select(skip => skip.Detail)];
    }

    /// <summary>
    /// Prepares a batch without writing it, and returns the rows the writer reports as merges with their log detail.
    /// </summary>
    public static async Task<IReadOnlyList<(string SourceId, string Detail)>> MergesFrom(IServiceProvider services, IMigrationCategoryWriter writer, MigrationBatch batch)
    {
        using var scope = services.CreateScope();
        var prepared = await writer.Prepare(scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>(), batch);

        return prepared.Merges;
    }

    /// <summary>
    /// The table and column a property is stored in, as this provider names them.
    /// </summary>
    public static string TableAndColumnOf<TEntity>(IServiceProvider services, string property)
    {
        using var scope = services.CreateScope();
        var entityType = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().Model.FindEntityType(typeof(TEntity))!;

        return $"{entityType.GetTableName()}.{entityType.FindProperty(property)!.GetColumnName()}";
    }
}
