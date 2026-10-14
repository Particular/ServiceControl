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
    public async Task A_value_is_measured_in_utf16_units_as_nvarchar_counts_them()
    {
        var fits = new KnownEndpoint { EndpointDetails = new EndpointDetails { Name = Repeat(225), HostId = Guid.NewGuid(), Host = "SALES01" } };
        var tooLong = new KnownEndpoint { EndpointDetails = new EndpointDetails { Name = Repeat(226), HostId = Guid.NewGuid(), Host = "SALES02" } };
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
            Assert.That(result.Copied, Is.EqualTo(1), "225 code points are 450 UTF-16 units, which an nvarchar(450) column holds");
            Assert.That(result.SkippedIds, Is.EqualTo(new[] { "KnownEndpoints/2" }), "226 code points are 452 UTF-16 units, which would fail the insert");
            Assert.That((await MonitoringDataStore.GetAllKnownEndpoints()).Select(endpoint => endpoint.EndpointDetails.Name), Is.EqualTo(new[] { Repeat(225) }));
            Assert.That(details, Is.EqualTo(new[]
            {
                $"{PreparedSkips.TableAndColumnOf<KnownEndpointEntity>(ServiceProvider, nameof(KnownEndpointEntity.Name))} takes at most 450 characters and the value holds 452, on document 'KnownEndpoints/2'"
            }), "the length reported is the one SQL Server counts");
            Assert.That(ServiceProvider.GetRequiredService<IMigrationSqlDialect>().TextLength(Repeat(226)), Is.LessThanOrEqualTo(Repeat(226).Length), "the check skips measuring a value whose UTF-16 length fits, which is safe only while no count exceeds it");
        }
    }

    [Test]
    public async Task A_value_holding_a_NUL_character_copies()
    {
        var withNul = new KnownEndpoint { EndpointDetails = new EndpointDetails { Name = "Sales", HostId = Guid.NewGuid(), Host = "SAL\u0000ES" } };
        var batch = new MigrationBatch([new MigrationRow("KnownEndpoints/1", withNul, new Dictionary<string, object>())], "KnownEndpoints/1");

        var result = await Target.Write(KnownEndpointsCategory, batch, new MigrationCheckpoint(MigrationCategoryIds.KnownEndpoints, MigrationCategoryState.InProgress, "KnownEndpoints/1", 0, 0, null, null, null, null, null, null));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1), "nvarchar stores U+0000, so a skip here would give up a row SQL Server can hold");
            Assert.That((await MonitoringDataStore.GetAllKnownEndpoints()).Single().EndpointDetails.Host, Is.EqualTo("SAL\u0000ES"));
        }
    }

    static string Repeat(int codePoints) => string.Concat(Enumerable.Repeat(OutsideTheBasicPlane, codePoints));
}
