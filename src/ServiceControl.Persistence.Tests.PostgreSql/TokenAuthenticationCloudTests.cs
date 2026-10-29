// ReSharper disable once CheckNamespace
namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NUnit.Framework;

[TestFixture]
class TokenAuthenticationCloudTests
{
    [Test]
    public async Task Migrates_round_trips_and_drops_the_schema_with_a_real_token()
    {
        var connectionString = TokenConnectionStrings.Cloud;

        if (string.IsNullOrEmpty(connectionString))
        {
            Assert.Ignore($"{TokenConnectionStrings.CloudVariable} is not set. The cloud database tests set it for the azure-postgresql and aurora-postgresql targets, see docs/cloud-database-tests.md.");
        }

        var settings = TokenAuthenticationTests.CreateSettings(connectionString, $"sc_token_{Guid.NewGuid():n}");
        await using var provider = TokenAuthenticationTests.BuildServiceProvider(settings);

        try
        {
            await TokenAuthenticationTests.ApplyMigrations(provider);
            await TokenAuthenticationTests.RoundTrip(provider);
        }
        finally
        {
            await TestSchema.Drop(provider.GetRequiredService<NpgsqlDataSource>(), settings.Schema);
        }
    }
}
