namespace ServiceControl.Persistence.EFCore.SqlServer;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ServiceControl.Persistence.EFCore.DbContexts;

/// <summary>
/// Design-time factory used by the dotnet-ef tooling (e.g. dotnet ef migrations add).
/// </summary>
public class SqlServerServiceControlDbContextFactory : IDesignTimeDbContextFactory<SqlServerServiceControlDbContext>
{
    public SqlServerServiceControlDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SERVICECONTROL_DATABASE_CONNECTIONSTRING")
            ?? "Server=localhost;Database=ServiceControl;Trusted_Connection=True;TrustServerCertificate=True";

        var optionsBuilder = new DbContextOptionsBuilder<SqlServerServiceControlDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sqlOptions => sqlOptions.MigrationsHistoryTable(ServiceControlDbContext.MigrationsHistoryTableName));

        return new SqlServerServiceControlDbContext(optionsBuilder.Options);
    }
}
