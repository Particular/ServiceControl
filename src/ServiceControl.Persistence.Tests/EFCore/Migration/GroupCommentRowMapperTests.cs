namespace ServiceControl.Persistence.Tests;

using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.DataMigration;
using ServiceControl.Persistence.EFCore.DbContexts;

class GroupCommentRowMapperTests : PersistenceTestBase
{
    [Test]
    public async Task Sets_every_mapped_property()
    {
        await using var scope = ServiceProvider.CreateAsyncScope();
        var model = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().Model;

        var entity = GroupCommentRowMapper.Map(new GroupComment { Id = "group-1", Comment = "Waiting on the payments team" });

        MigrationEntityCoverage.AssertEveryMappedPropertyIsSet(model, entity);
    }
}
