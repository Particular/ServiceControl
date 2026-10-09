namespace ServiceControl.Persistence.Tests;

using System.Linq;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.DbContexts;

class MigrationEntityCoverageClaimTests : PersistenceTestBase
{
    [Test]
    public void Every_property_the_database_fills_in_is_a_key_it_assigns_on_insert()
    {
        using var scope = ServiceProvider.CreateScope();
        var model = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().Model;

        var databaseFilled = model.GetEntityTypes()
            .SelectMany(entityType => entityType.GetProperties())
            .Where(property => property.ValueGenerated != ValueGenerated.Never)
            .ToArray();

        var notKeysAssignedOnInsert = databaseFilled
            .Where(property => !property.IsPrimaryKey() || property.ValueGenerated != ValueGenerated.OnAdd)
            .Select(property => $"{property.DeclaringType.ClrType.Name}.{property.Name} is {property.ValueGenerated}")
            .ToArray();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(databaseFilled, Is.Not.Empty, "the model no longer has a single property the database fills in, so this test is watching nothing. Either the identity keys have gone, or the model was never built.");
            Assert.That(notKeysAssignedOnInsert, Is.Empty, "MigrationEntityCoverage checks only the properties that are ValueGenerated.Never, on the claim that every other one is a key the database assigns on insert. These properties break that claim, so the coverage check silently stops watching them and a migration can copy a row with them left unset.");
        }
    }
}
