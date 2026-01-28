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

        // MySQL full-text search optimization
        // Add FULLTEXT index on both HeadersJson and Body columns for multi-column search
        var failedMessages = modelBuilder.Entity<Core.Entities.FailedMessageEntity>();

        failedMessages
            .HasIndex(e => new { e.HeadersJson, e.Body })
            .HasAnnotation("MySql:FullTextIndex", true);
    }
}
