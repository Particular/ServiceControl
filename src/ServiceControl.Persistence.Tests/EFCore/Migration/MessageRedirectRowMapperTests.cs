namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.DataMigration;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.MessageRedirects;

class MessageRedirectRowMapperTests : PersistenceTestBase
{
    [Test]
    public async Task Sets_every_mapped_property()
    {
        await using var scope = ServiceProvider.CreateAsyncScope();
        var model = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().Model;

        var entity = MessageRedirectRowMapper.Map(new MessageRedirect
        {
            FromPhysicalAddress = "Sales@OLDHOST",
            ToPhysicalAddress = "Sales@NEWHOST",
            LastModified = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc)
        });

        MigrationEntityCoverage.AssertEveryMappedPropertyIsSet(model, entity);
    }
}
