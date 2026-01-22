namespace ServiceControl.Audit.Persistence.Sql.SqlServer;

using Core.DbContexts;
using Microsoft.EntityFrameworkCore;

public class SqlServerAuditDbContext : AuditDbContextBase
{
    public SqlServerAuditDbContext(DbContextOptions<SqlServerAuditDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreatingProvider(ModelBuilder modelBuilder)
    {
        // Convert PostgreSQL jsonb types to SQL Server nvarchar(max)
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
    }
}
