namespace ServiceControl.Audit.Persistence.EFCore.Infrastructure;

using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

public static class SchemaQualifiedTableName
{
    public static string For<TEntity>(DbContext dbContext) =>
        cache.GetOrAdd((dbContext.Model, typeof(TEntity)), static (key, context) =>
        {
            var entityType = key.Model.FindEntityType(key.EntityType)
                ?? throw new InvalidOperationException($"{key.EntityType.Name} is not part of the model.");

            var tableName = entityType.GetTableName()
                ?? throw new InvalidOperationException($"{key.EntityType.Name} is not mapped to a table.");

            return context.GetService<ISqlGenerationHelper>().DelimitIdentifier(tableName, entityType.GetSchema());
        }, dbContext);

    static readonly ConcurrentDictionary<(IModel Model, Type EntityType), string> cache = new();
}
