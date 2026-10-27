namespace ServiceControl.Persistence.EFCore.SqlServer;

using EntityConfigurations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;

public class SqlServerServiceControlDbContext(DbContextOptions<SqlServerServiceControlDbContext> options) : ServiceControlDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new FailedMessageConfiguration());
        modelBuilder.ApplyConfiguration(new FailedMessageGroupConfiguration());
    }

    public override bool IsDuplicateKeyException(DbUpdateException exception)
    {
        for (var inner = exception.InnerException; inner != null; inner = inner.InnerException)
        {
            if (inner is SqlException { Number: 2601 or 2627 })
            {
                return true;
            }
        }
        return false;
    }

    protected override async Task<bool> TableExists(string table, CancellationToken cancellationToken = default) =>
        await Database
            .SqlQueryRaw<int>("SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.tables WHERE name = {0} AND schema_id = SCHEMA_ID(COALESCE(CAST({1} AS sysname), SCHEMA_NAME()))) THEN 1 ELSE 0 END AS [Value]", table, Schema ?? (object)DBNull.Value)
            .SingleAsync(cancellationToken) == 1;
}
