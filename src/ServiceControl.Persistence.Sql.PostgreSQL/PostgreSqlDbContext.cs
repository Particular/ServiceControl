namespace ServiceControl.Persistence.Sql.PostgreSQL;

using Core.DbContexts;
using Microsoft.EntityFrameworkCore;

class PostgreSqlDbContext : ServiceControlDbContextBase
{
    public PostgreSqlDbContext(DbContextOptions<PostgreSqlDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply lowercase naming convention for PostgreSQL
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            entity.SetTableName(entity.GetTableName()?.ToLowerInvariant());

            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(property.GetColumnName().ToLowerInvariant());
            }
        }

        base.OnModelCreating(modelBuilder);
    }

    protected override void OnModelCreatingProvider(ModelBuilder modelBuilder)
    {
        // PostgreSQL-specific configurations if needed
    }
}
