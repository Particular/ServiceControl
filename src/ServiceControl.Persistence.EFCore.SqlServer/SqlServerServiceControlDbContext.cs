namespace ServiceControl.Persistence.EFCore.SqlServer;

using Microsoft.EntityFrameworkCore;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;

public class SqlServerServiceControlDbContext(DbContextOptions<SqlServerServiceControlDbContext> options) : ServiceControlDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FailedMessageEntity>()
            .HasIndex(e => e.StatusChangedAt)
            .HasFilter($"[Status] IN ({(int)FailedMessageStatus.Resolved}, {(int)FailedMessageStatus.Archived})");
    }
}
