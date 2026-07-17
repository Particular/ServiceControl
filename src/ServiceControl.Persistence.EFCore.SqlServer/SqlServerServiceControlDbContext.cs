namespace ServiceControl.Persistence.EFCore.SqlServer;

using Microsoft.EntityFrameworkCore;
using ServiceControl.Persistence.EFCore.DbContexts;

public class SqlServerServiceControlDbContext(DbContextOptions<SqlServerServiceControlDbContext> options) : ServiceControlDbContext(options)
{
    protected override void OnModelCreatingProvider(ModelBuilder modelBuilder)
    {
    }
}
