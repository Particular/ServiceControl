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
        // MySQL-specific configurations if needed
    }
}
