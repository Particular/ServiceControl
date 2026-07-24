namespace ServiceControl.Persistence.EFCore.PostgreSql;

using Microsoft.EntityFrameworkCore;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;

public class PostgreSqlServiceControlDbContext(DbContextOptions<PostgreSqlServiceControlDbContext> options) : ServiceControlDbContext(options)
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // Use snake_case naming convention for PostgreSQL
        optionsBuilder.UseSnakeCaseNamingConvention();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FailedMessageEntity>()
            .HasIndex(e => e.StatusChangedAt)
            .HasFilter($"status IN ({(int)FailedMessageStatus.Resolved}, {(int)FailedMessageStatus.Archived})");
    }
}
