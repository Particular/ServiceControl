namespace ServiceControl.Persistence.Sql.MySQL;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

// Design-time factory for EF Core tools (migrations, etc.)
class MySqlDbContextFactory : IDesignTimeDbContextFactory<MySqlDbContext>
{
    public MySqlDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MySqlDbContext>();

        // Use a dummy connection string for design-time operations
        var connectionString = "Server=localhost;Database=servicecontrol;User=root;Password=mysql";
        // Use MySQL 8.0 as the server version for migrations
        optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0)));

        return new MySqlDbContext(optionsBuilder.Options);
    }
}
