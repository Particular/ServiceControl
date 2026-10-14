namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Operations;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.DataMigration.Writers;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;

class MigrationValueLengthTests : PersistenceTestBase
{
    static readonly string OutsideTheBasicPlane = char.ConvertFromUtf32(0x1F600);

    static readonly MigrationCategory KnownEndpointsCategory = MigrationCategoryRegistry.All.Single(category => category.Id == MigrationCategoryIds.KnownEndpoints);

    IMigrationTarget Target => ServiceProvider.GetRequiredService<IMigrationTarget>();

    [SetUp]
    public Task OpenTarget() => Target.Open();

    [Test]
    public async Task A_value_is_measured_in_code_points_as_varchar_counts_them()
    {
        var fits = new KnownEndpoint { EndpointDetails = new EndpointDetails { Name = Repeat(450), HostId = Guid.NewGuid(), Host = "SALES01" } };
        var tooLong = new KnownEndpoint { EndpointDetails = new EndpointDetails { Name = Repeat(451), HostId = Guid.NewGuid(), Host = "SALES02" } };
        var batch = new MigrationBatch(
            [
                new MigrationRow("KnownEndpoints/1", fits, new Dictionary<string, object>()),
                new MigrationRow("KnownEndpoints/2", tooLong, new Dictionary<string, object>())
            ],
            "KnownEndpoints/2");

        var details = await PreparedSkips.DetailsFrom(ServiceProvider, new KnownEndpointsWriter(ServiceProvider.GetRequiredService<IMigrationSqlDialect>()), batch);
        var result = await Target.Write(KnownEndpointsCategory, batch, new MigrationCheckpoint(MigrationCategoryIds.KnownEndpoints, MigrationCategoryState.InProgress, "KnownEndpoints/2", 0, 0, null, null, null, null, null, null));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1), "450 code points fit a varchar(450) column though they are 900 UTF-16 units, and a skip here would be permanent");
            Assert.That(result.SkippedIds, Is.EqualTo(new[] { "KnownEndpoints/2" }));
            Assert.That((await MonitoringDataStore.GetAllKnownEndpoints()).Select(endpoint => endpoint.EndpointDetails.Name), Is.EqualTo(new[] { Repeat(450) }));
            Assert.That(details, Is.EqualTo(new[]
            {
                $"{PreparedSkips.TableAndColumnOf<KnownEndpointEntity>(ServiceProvider, nameof(KnownEndpointEntity.Name))} takes at most 450 characters and the value holds 451, on document 'KnownEndpoints/2'"
            }), "the length reported is the one PostgreSQL counts");
            Assert.That(ServiceProvider.GetRequiredService<IMigrationSqlDialect>().TextLength(Repeat(451)), Is.LessThanOrEqualTo(Repeat(451).Length), "the check skips measuring a value whose UTF-16 length fits, which is safe only while no count exceeds it");
        }
    }

    [Test]
    public async Task A_value_holding_a_NUL_character_is_skipped_as_malformed_naming_the_column()
    {
        var clean = new KnownEndpoint { EndpointDetails = new EndpointDetails { Name = "Sales", HostId = Guid.NewGuid(), Host = "SALES01" } };
        var withNul = new KnownEndpoint { EndpointDetails = new EndpointDetails { Name = "Sales", HostId = Guid.NewGuid(), Host = "SAL\u0000ES" } };
        var batch = new MigrationBatch(
            [
                new MigrationRow("KnownEndpoints/1", clean, new Dictionary<string, object>()),
                new MigrationRow("KnownEndpoints/2", withNul, new Dictionary<string, object>())
            ],
            "KnownEndpoints/2");

        var details = await PreparedSkips.DetailsFrom(ServiceProvider, new KnownEndpointsWriter(ServiceProvider.GetRequiredService<IMigrationSqlDialect>()), batch);
        var result = await Target.Write(KnownEndpointsCategory, batch, new MigrationCheckpoint(MigrationCategoryIds.KnownEndpoints, MigrationCategoryState.InProgress, "KnownEndpoints/2", 0, 0, null, null, null, null, null, null));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1), "PostgreSQL refuses U+0000 in text, which would fail the whole insert and halt the category");
            Assert.That(result.SkipReasons, Is.EqualTo(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.MalformedValue] = 1 }));
            Assert.That(details, Is.EqualTo(new[]
            {
                $"{PreparedSkips.TableAndColumnOf<KnownEndpointEntity>(ServiceProvider, nameof(KnownEndpointEntity.Host))} cannot hold the character U+0000, which the value contains, on document 'KnownEndpoints/2'"
            }));
        }
    }

    static string Repeat(int codePoints) => string.Concat(Enumerable.Repeat(OutsideTheBasicPlane, codePoints));
}
