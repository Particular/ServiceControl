namespace ServiceControl.Persistence.Sql.MySQL;

using Core.DbContexts;
using Microsoft.EntityFrameworkCore;

class MySqlDbContext : ServiceControlDbContextBase
{
    public MySqlDbContext(DbContextOptions<MySqlDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreatingProvider(ModelBuilder modelBuilder)
    {
        // MySQL uses 'json' instead of 'jsonb' (PostgreSQL-specific)
        // Override all jsonb column types to use 'json'
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.GetColumnType() == "jsonb")
                {
                    property.SetColumnType("json");
                }
            }
        }
    }
}
