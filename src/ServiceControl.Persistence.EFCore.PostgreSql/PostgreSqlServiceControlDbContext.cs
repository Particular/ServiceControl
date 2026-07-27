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
