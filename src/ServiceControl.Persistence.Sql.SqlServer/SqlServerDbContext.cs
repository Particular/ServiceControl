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
        // SQL Server-specific configurations if needed
    }
}
