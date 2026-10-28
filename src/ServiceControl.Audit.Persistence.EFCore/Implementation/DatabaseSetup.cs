namespace ServiceControl.Audit.Persistence.EFCore.Implementation;

using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceControl.Audit.Persistence.EFCore.Abstractions;
using ServiceControl.Audit.Persistence.EFCore.DbContexts;
using ServiceControl.Audit.Persistence.EFCore.Implementation.BodyStorage;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure;

sealed class DatabaseSetup(
    IServiceScopeFactory scopeFactory,
    IAuditPartitionManager partitions,
    EFPersisterSettings settings,
    TimeProvider timeProvider,
    ILogger<DatabaseSetup> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (settings.BodyStorage is not null)
        {
            await ProvisionBodyStorage(settings.BodyStorage, cancellationToken);
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        dbContext.Database.SetCommandTimeout(EFPersisterSettings.MigrationCommandTimeout);

        await dbContext.RequireNoPrimaryMigrationsHistory(cancellationToken);

        logger.LogInformation("Starting audit database migration");
        await dbContext.Database.MigrateAsync(cancellationToken);

        var hour = AuditHours.Truncate(timeProvider.GetUtcNow().UtcDateTime);
        await partitions.EnsurePartitions(dbContext, hour.AddHours(-1), hour + AuditHours.Lookahead, cancellationToken);
        logger.LogInformation("Audit database migration completed");
    }

    static Task ProvisionBodyStorage(BodyStorageSettings bodyStorage, CancellationToken cancellationToken)
    {
        switch (bodyStorage)
        {
            case FileSystemBodyStorageSettings fileSystem:
                cancellationToken.ThrowIfCancellationRequested();
                Directory.CreateDirectory(fileSystem.StoragePath);
                return Task.CompletedTask;
            case AzureBlobBodyStorageSettings azureBlob:
                return AuditAzureBlobClientFactory.CreateContainerClient(azureBlob).CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            case S3BodyStorageSettings s3:
                return ProvisionS3Bucket(s3, cancellationToken);
            default:
                throw new ArgumentOutOfRangeException(nameof(bodyStorage), bodyStorage, "Unknown body storage type.");
        }
    }

    static async Task ProvisionS3Bucket(S3BodyStorageSettings s3, CancellationToken cancellationToken)
    {
        using var client = AuditS3ClientFactory.Create(s3);

        if (!await AmazonS3Util.DoesS3BucketExistV2Async(client, s3.BucketName))
        {
            await client.PutBucketAsync(new PutBucketRequest { BucketName = s3.BucketName }, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
