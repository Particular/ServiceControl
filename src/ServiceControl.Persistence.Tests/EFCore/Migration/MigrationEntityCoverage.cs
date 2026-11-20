namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections;
using System.Linq;
using Microsoft.EntityFrameworkCore.Metadata;
using NUnit.Framework;

static class MigrationEntityCoverage
{
    public static void AssertEveryMappedPropertyIsSet<TEntity>(IModel model, TEntity entity, params string[] legitimatelyDefault)
    {
        var entityType = model.FindEntityType(typeof(TEntity))
            ?? throw new ArgumentException($"{typeof(TEntity).Name} is not an entity in the EF Core model.", nameof(entity));

        // Reflection rather than a new() constraint, which an entity with required members cannot satisfy.
        var defaults = Activator.CreateInstance(typeof(TEntity));

        // Every property in this model that is not ValueGenerated.Never is an identity column the database assigns.
        var unset = entityType.GetProperties()
            .Where(property => property.ValueGenerated == ValueGenerated.Never && !legitimatelyDefault.Contains(property.Name))
            .Where(property => SameValue(property.GetGetter().GetClrValue(entity), property.GetGetter().GetClrValue(defaults)))
            .Select(property => property.Name)
            .ToArray();

        Assert.That(unset, Is.Empty, $"{typeof(TEntity).Name} has mapped properties equal to a default-constructed {typeof(TEntity).Name}. Set each from the source document, feed the test a document whose values all differ from those defaults, or name the property in legitimatelyDefault if RavenDB genuinely holds that value.");
    }

    // Collections compare by item, and an empty one is unset whether or not the entity initialises it.
    static bool SameValue(object actual, object defaultValue) =>
        actual is IEnumerable items and not string
            ? !items.Cast<object>().Any() || (defaultValue is IEnumerable defaultItems && items.Cast<object>().SequenceEqual(defaultItems.Cast<object>()))
            : Equals(actual, defaultValue);
}
