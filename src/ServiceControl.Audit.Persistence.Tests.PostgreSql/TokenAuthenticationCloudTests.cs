namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Npgsql;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.Infrastructure;
    using ServiceControl.Persistence.Tests;

    [TestFixture]
    class TokenAuthenticationCloudTests
    {
        [Test]
        public async Task Migrates_round_trips_locks_and_drops_the_schema_with_a_real_token()
        {
            var connectionString = TokenConnectionStrings.Cloud;

            if (string.IsNullOrEmpty(connectionString))
            {
                Assert.Ignore($"{TokenConnectionStrings.CloudVariable} is not set. The cloud database tests set it for the azure-postgresql and aurora-postgresql targets, see docs/cloud-database-tests.md.");
            }

            var schema = $"sc_token_{Guid.NewGuid():n}";
            await using var provider = TokenAuthenticationTests.BuildServiceProvider(connectionString, schema);

            try
            {
                await TokenAuthenticationTests.ApplyMigrations(provider);
                await TokenAuthenticationTests.RoundTrip(provider);

                var retentionLock = await provider.GetRequiredService<IRetentionLock>().TryAcquire();
                Assert.That(retentionLock, Is.Not.Null);
                await retentionLock.DisposeAsync();
            }
            finally
            {
                await TestSchema.Drop(provider.GetRequiredService<NpgsqlDataSource>(), schema);
            }
        }
    }
}
