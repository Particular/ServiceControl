namespace ServiceControl.Persistence.EFCore.PostgreSql;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

/// <summary>
/// Design-time factory used by the dotnet-ef tooling (e.g. dotnet ef migrations add).
/// </summary>
public class PostgreSqlServiceControlDbContextFactory : IDesignTimeDbContextFactory<PostgreSqlServiceControlDbContext>
{
    public PostgreSqlServiceControlDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SERVICECONTROL_DATABASE_CONNECTIONSTRING")
            ?? "Host=localhost;Port=5432;Database=servicecontrol;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<PostgreSqlServiceControlDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new PostgreSqlServiceControlDbContext(optionsBuilder.Options);
    }
}
