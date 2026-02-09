namespace ServiceControl.Persistence.Sql.SqlServer;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

// Design-time factory for EF Core tools (migrations, etc.)
class SqlServerDbContextFactory : IDesignTimeDbContextFactory<SqlServerDbContext>
{
    public SqlServerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SqlServerDbContext>();

        // Use a dummy connection string for design-time operations
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ServiceControl;Trusted_Connection=True;");

        return new SqlServerDbContext(optionsBuilder.Options);
    }
}
