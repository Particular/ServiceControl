namespace ServiceControl.Persistence.EFCore.PostgreSql;

using Microsoft.EntityFrameworkCore;
using Npgsql;
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

        // Widen the group-aggregate indexes with covering INCLUDE columns so the
        // /api/recoverability/groups/ aggregate is index-only (no key lookups / sequential scan with a
        // residual status predicate). See missing-indexes.md. The IncludeProperties API is
        // provider-specific, so the widening is applied here rather than in the shared configuration.
        modelBuilder.Entity<FailedMessageEntity>()
            .HasIndex(e => new { e.Status, e.LastModified })
            .IncludeProperties(nameof(FailedMessageEntity.FirstTimeOfFailure), nameof(FailedMessageEntity.LastTimeOfFailure));

        modelBuilder.Entity<FailedMessageGroupEntity>()
            .HasIndex(e => new { e.Type, e.GroupId })
            .IncludeProperties(nameof(FailedMessageGroupEntity.Title));
    }

    public override bool IsDuplicateKeyException(DbUpdateException exception)
    {
        var queue = new Queue<Exception>([exception]);
        while (queue.Count > 0)
        {
            var e = queue.Dequeue();
            if (e is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return true;
            }

            //it is unlikely, but there are cases where postgres EF throws aggregate exceptions
            if (e is AggregateException aggregateException)
            {
                foreach (var inner in aggregateException.InnerExceptions)
                {
                    queue.Enqueue(inner);
                }
            }

            if (e.InnerException != null)
            {
                queue.Enqueue(e.InnerException);
            }
        }
        return false;
    }
}
