namespace ServiceControl.Persistence.EFCore.Infrastructure;

using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

/// <summary>
/// The delimited, schema qualified table name an entity is mapped to, for the raw SQL the dialects
/// build. Taking the name from the model rather than writing it out keeps the dialects correct
/// when the schema is configured, and keeps them from restating the naming convention that decides
/// the table names in the first place.
/// </summary>
public static class SchemaQualifiedTableName
{
    public static string For<TEntity>(DbContext dbContext) =>
        // The model cache key includes the schema, so each schema has its own model and therefore
        // its own entry here.
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
