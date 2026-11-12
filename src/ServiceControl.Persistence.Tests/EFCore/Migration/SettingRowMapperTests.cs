namespace ServiceControl.Persistence.Tests;

using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;
using ServiceControl.Persistence.EFCore.DataMigration;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Infrastructure;

class SettingRowMapperTests : PersistenceTestBase
{
    static readonly List<string> ReportMasks = ["contoso", "fabrikam"];

    static readonly LicensedEndpointDetails Details = new LicensedEndpointDetails
    {
        LicenseId = "abc-123",
        ValidId = true,
        ServiceEndDate = "2027-01-01",
        Endpoints = [new LicensedEndpoint { Name = "Sales.Orders", Classification = 2, EndpointSize = "Large", Queues = [new QueueIdentity { NameHash = "n1", ScopeHash = "s1" }] }],
        InfrastructureQueues = [new QueueIdentity { NameHash = "n2", ScopeHash = "s2" }],
        ExcludedQueues = [new QueueIdentity { NameHash = "n3", ScopeHash = "s3" }],
        Products = [new Product { ProductCode = "P1", MonthlyThroughput = 500 }]
    };

    [Test]
    public async Task Sets_every_mapped_property()
    {
        await using var scope = ServiceProvider.CreateAsyncScope();
        var model = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().Model;

        MigrationEntityCoverage.AssertEveryMappedPropertyIsSet(model, SettingRowMapper.Map(SettingKeys.ReportMasks, ReportMasks));
    }

    [Test]
    public async Task Licensing_settings_read_back_through_GetSetting_as_the_product_reads_them()
    {
        await using (var scope = ServiceProvider.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
            dbContext.Settings.AddRange(SettingRowMapper.Map(SettingKeys.ReportMasks, ReportMasks), SettingRowMapper.Map(SettingKeys.LicensedEndpointDetails, Details));
            await dbContext.SaveChangesAsync();
        }

        await using (var scope = ServiceProvider.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

            Assert.That(await dbContext.GetSetting<List<string>>(SettingKeys.ReportMasks), Is.EqualTo(ReportMasks));
            Assert.That(JsonSerializer.Serialize(await dbContext.GetSetting<LicensedEndpointDetails>(SettingKeys.LicensedEndpointDetails)), Is.EqualTo(JsonSerializer.Serialize(Details)));
        }
    }
}
