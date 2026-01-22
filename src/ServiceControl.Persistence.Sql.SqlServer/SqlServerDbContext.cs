namespace ServiceControl.Persistence.Sql.SqlServer;

using Core.DbContexts;
using Microsoft.EntityFrameworkCore;

class SqlServerDbContext : ServiceControlDbContextBase
{
    public SqlServerDbContext(DbContextOptions<SqlServerDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreatingProvider(ModelBuilder modelBuilder)
    {
        // SQL Server stores JSON as nvarchar(max), not jsonb (PostgreSQL-specific)
        // Override all jsonb column types to use 'nvarchar(max)'
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.GetColumnType() == "jsonb")
                {
                    property.SetColumnType("nvarchar(max)");
                }
            }
        }

        // SQL Server full-text search optimization
        // Note: FULLTEXT indexes must be created via migration SQL since EF Core doesn't have direct support
        // The migration should include:
        // CREATE FULLTEXT INDEX ON FailedMessages(HeadersJson, Body)
        //   KEY INDEX PK_FailedMessages;
    }
}
