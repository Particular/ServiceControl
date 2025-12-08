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
        optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

        return new MySqlDbContext(optionsBuilder.Options);
    }
}
