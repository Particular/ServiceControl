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
        // Use a fixed server version for design-time to avoid connection attempts
        optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 21)));

        return new MySqlDbContext(optionsBuilder.Options);
    }
}
