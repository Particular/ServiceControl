namespace ServiceControl.Persistence.EFCore.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ServiceControl.Persistence.EFCore.DbContexts;

/// <summary>
/// EF Core keys its model cache on the context type alone, so contexts configured with different
/// schemas would share one model and every one after the first would read and write the wrong
/// schema's tables.
/// </summary>
public sealed class SchemaModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) =>
        (context.GetType(), (context as ServiceControlDbContext)?.Schema, designTime);
}
