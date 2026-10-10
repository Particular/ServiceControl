namespace ServiceControl.Audit.Persistence.EFCore.PostgreSql;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;
using ServiceControl.Audit.Persistence.EFCore.DbContexts;

public class PostgreSqlAuditDbContextFactory : IDesignTimeDbContextFactory<PostgreSqlAuditDbContext>
{
    public PostgreSqlAuditDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SERVICECONTROL_DATABASE_CONNECTIONSTRING")
            ?? "Host=localhost;Port=5432;Database=servicecontrolaudit;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<PostgreSqlAuditDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.MigrationsHistoryTable(AuditDbContext.MigrationsHistoryTableName));
        optionsBuilder.ReplaceService<IMigrationsSqlGenerator, AuditNpgsqlMigrationsSqlGenerator>();

        return new PostgreSqlAuditDbContext(optionsBuilder.Options);
    }
}
