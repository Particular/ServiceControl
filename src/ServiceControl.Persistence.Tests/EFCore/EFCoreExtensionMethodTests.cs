namespace DefaultNamespace;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.Tests;

public class EFCoreExtensionMethodTests : PersistenceTestBase
{
    [Test, CancelAfter(60_000)]
    public async Task Insert_until_conflict_should_not_throw_errors(CancellationToken cancellationToken)
    {
        var i = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            i++;
            var key = Guid.NewGuid();
            var result = await Task.WhenAll(Write(key), Write(key), Write(key));
            if (result.Contains(Result.ConflictUpdate))
            {
                Assert.Pass($"Test had concurrency failure ({i}): {string.Join(", ", result)}");
            }
        }

        Assert.Fail("Test never had concurrency failure");

        async Task<Result> Write(Guid key)
        {
            await using var scope = ServiceProvider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

            var state = Result.Update;
            await context.UpsertAsync([key],
                () =>
                {
                    state = Result.Insert;
                    return new FailedMessageEntity
                    {
                        UniqueMessageId = key,
                        HeadersJson = "blah"
                    };
                },
                es =>
                {
                    state = state == Result.Insert ? Result.ConflictUpdate : Result.Update;
                    es.BodyText = Guid.NewGuid().ToString();
                },
                cancellationToken: cancellationToken);
            return state;
        }
    }
       
    public enum Result
    {
        Insert,
        Update,
        ConflictUpdate
    }
}