namespace ServiceControl.Persistence.EFCore.SqlServer;

using System;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;

public class SqlServerServiceControlDbContext(DbContextOptions<SqlServerServiceControlDbContext> options) : ServiceControlDbContext(options)
{
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FailedMessageEntity>()
            .HasIndex(e => e.StatusChangedAt)
            .HasFilter($"[Status] IN ({(int)FailedMessageStatus.Resolved}, {(int)FailedMessageStatus.Archived})");
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
}
