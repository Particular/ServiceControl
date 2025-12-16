namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServiceControl.Operations;
using ServiceControl.Persistence;
using Microsoft.Extensions.DependencyInjection;

public class FailedErrorImportDataStore : DataStoreBase, IFailedErrorImportDataStore
{
    readonly ILogger<FailedErrorImportDataStore> logger;

    public FailedErrorImportDataStore(IServiceScopeFactory scopeFactory, ILogger<FailedErrorImportDataStore> logger) : base(scopeFactory)
    {
        this.logger = logger;
    }

    public Task ProcessFailedErrorImports(Func<FailedTransportMessage, Task> processMessage, CancellationToken cancellationToken)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var succeeded = 0;
            var failed = 0;

            var imports = dbContext.FailedErrorImports.AsAsyncEnumerable();

            await foreach (var import in imports.WithCancellation(cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                FailedTransportMessage? transportMessage = null;
                try
                {
                    transportMessage = JsonSerializer.Deserialize<FailedTransportMessage>(import.MessageJson, JsonSerializationOptions.Default);

                    Debug.Assert(transportMessage != null, "Deserialized transport message should not be null");

                    await processMessage(transportMessage);

                    dbContext.FailedErrorImports.Remove(import);
                    await dbContext.SaveChangesAsync(cancellationToken);

                    succeeded++;
                    logger.LogDebug("Successfully re-imported failed error message {MessageId}", transportMessage.Id);
                }
                catch (OperationCanceledException e) when (cancellationToken.IsCancellationRequested)
                {
                    logger.LogInformation(e, "Cancelled");
                    break;
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error while attempting to re-import failed error message {MessageId}", transportMessage?.Id ?? "unknown");
                    failed++;
                }
            }

            logger.LogInformation("Done re-importing failed errors. Successfully re-imported {SucceededCount} messages. Failed re-importing {FailedCount} messages", succeeded, failed);

            if (failed > 0)
            {
                logger.LogWarning("{FailedCount} messages could not be re-imported. This could indicate a problem with the data. Contact Particular support if you need help with recovering the messages", failed);
            }
        });
    }

    public Task<bool> QueryContainsFailedImports()
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            return await dbContext.FailedErrorImports.AnyAsync();
        });
    }
}
