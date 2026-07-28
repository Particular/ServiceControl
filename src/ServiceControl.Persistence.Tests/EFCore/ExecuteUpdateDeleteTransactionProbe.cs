namespace ServiceControl.Persistence.Tests;

using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.DbContexts;

// Verifies the assumption the batch writer's atomicity rests on: ExecuteUpdate/ExecuteDelete run
// inside the ambient transaction, so a rollback undoes them.
class ExecuteUpdateDeleteTransactionProbe : ErrorIngestionTestBase
{
    [Test]
    public async Task ExecuteDelete_and_ExecuteUpdate_are_undone_by_a_rollback()
    {
        var failure = new IngestedFailure();
        await Ingest(failure);

        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync();

            await dbContext.FailedMessages
                .Where(m => m.UniqueMessageId == failure.UniqueMessageId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.ExceptionMessage, "changed in the transaction"));

            await dbContext.FailedMessages
                .Where(m => m.UniqueMessageId == failure.UniqueMessageId)
                .ExecuteDeleteAsync();

            await transaction.RollbackAsync();
        });

        var row = await FindFailedMessage(failure.UniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row, Is.Not.Null, "ExecuteDelete must enlist in the transaction, so a rollback keeps the row");
            Assert.That(row!.ExceptionMessage, Is.EqualTo(failure.ExceptionMessage), "ExecuteUpdate must enlist too, so a rollback reverts the change");
        }
    }
}
