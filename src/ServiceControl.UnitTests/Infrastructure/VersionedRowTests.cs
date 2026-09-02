#nullable enable
namespace ServiceControl.UnitTests.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using NUnit.Framework;
    using Raven.Client.Documents.Conventions;
    using ServiceControl.EventLog;
    using ServiceControl.Infrastructure.WebApi;
    using ServiceControl.Operations;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Infrastructure;
    using Sparrow.Json;

    [TestFixture]
    public class VersionedRowTests
    {
        static IEnumerable<Type> RowTypes() =>
            new[] { typeof(EventLogItemView).Assembly, typeof(SerializerOptions).Assembly }
                .SelectMany(Types)
                .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                               && typeof(IVersionedRow).IsAssignableFrom(type))
                .OrderBy(type => type.FullName, StringComparer.Ordinal);

        [Test]
        public void The_rows_are_found_at_all()
        {
            Assert.That(RowTypes(), Does.Contain(typeof(EventLogItemView)),
                "the scan below found nothing, so every other test here would pass without checking anything");
        }

        // RavenDB serialises explicit interface implementations, under their fully qualified name, where
        // System.Text.Json ignores them. Some of these rows are stored as-is, so a computed member lands in the document.
        [TestCaseSource(nameof(RowTypes))]
        public void Only_the_rows_own_fields_are_stored(Type type)
        {
            var conventions = new DocumentConventions();
            conventions.Serialization.Initialize(conventions);

            using var context = JsonOperationContext.ShortTermSingleUse();
            using var stored = conventions.Serialization.DefaultConverter.ToBlittable(Seeded(type), context);

            Assert.That(stored.GetPropertyNames(), Is.EquivalentTo(Stored(type)),
                $"{type.Name} would be written to the database with something other than its own properties, so a value computed from the fields beside it gets stored next to them");
        }

        [TestCaseSource(nameof(RowTypes))]
        public void Every_covered_field_can_be_formatted(Type type) =>
            Assert.That(() => VersionOf(Seeded(type)), Throws.Nothing);

        [TestCaseSource(nameof(RowTypes))]
        public void Changing_any_rendered_field_moves_the_version(Type type)
        {
            var row = Seeded(type);
            var rendered = Rendered(type);

            Assert.That(rendered, Is.Not.Empty, "nothing to check means the reflection above stopped working");

            using (Assert.EnterMultipleScope())
            {
                foreach (var property in rendered)
                {
                    var before = VersionOf(row);
                    var original = property.GetValue(row);

                    property.SetValue(row, Different(property.PropertyType, original));

                    Assert.That(VersionOf(row).Matches(before), Is.False,
                        $"{type.Name}.{property.Name} is rendered but not covered, so it can change while the validator holds still and the client keeps its stale page for ever");

                    property.SetValue(row, original);
                }
            }
        }

        static DataVersion VersionOf(IVersionedRow row) => DataVersion.OverRows([("rows", 1)], [row]);

        static PropertyInfo[] Rendered(Type type) =>
            [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.CanWrite)];

        // Read-only ones count: a serialiser stores them too, and they are legitimate data.
        static string[] Stored(Type type) =>
            [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => property.Name)];

        // Every field set to something, so a row is never checked with a null standing in for a real value
        // and a computed property never has to cope with an unset one.
        static IVersionedRow Seeded(Type type)
        {
            var row = Activator.CreateInstance(type) as IVersionedRow
                      ?? throw new NotSupportedException(
                          $"{type.Name} cannot be created without arguments, so this test cannot build one to check.");

            foreach (var property in Rendered(type))
            {
                property.SetValue(row, Different(property.PropertyType, null));
            }

            return row;
        }

        static object? Different(Type type, object? current)
        {
            var bare = Nullable.GetUnderlyingType(type) ?? type;

            if (bare.IsEnum)
            {
                var values = Enum.GetValues(bare).Cast<object>().Where(value => !value.Equals(current)).ToArray();

                return values.Length > 0
                    ? values[0]
                    : throw new NotSupportedException($"{bare.Name} has only one value, so a change to it cannot be observed.");
            }

            if (bare == typeof(string))
            {
                return (string?)current + "-changed";
            }

            if (bare == typeof(DateTime))
            {
                return (current as DateTime?)?.AddDays(1) ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            }

            if (bare == typeof(Guid))
            {
                var first = Guid.Parse("11111111-1111-1111-1111-111111111111");

                return (current as Guid?) == first ? Guid.Parse("22222222-2222-2222-2222-222222222222") : first;
            }

            if (bare == typeof(bool))
            {
                return !(current as bool? ?? false);
            }

            if (bare.IsPrimitive)
            {
                return Convert.ChangeType(Convert.ToDouble(current) + 1, bare);
            }

            if (typeof(IEnumerable<string>).IsAssignableFrom(bare))
            {
                return new List<string>((current as IEnumerable<string>) ?? []) { "added" };
            }

            if (bare == typeof(EndpointDetails))
            {
                var endpoint = current as EndpointDetails;

                return new EndpointDetails
                {
                    Name = (endpoint?.Name ?? string.Empty) + "-changed",
                    Host = endpoint?.Host ?? string.Empty,
                    HostId = endpoint?.HostId ?? Guid.Empty
                };
            }

            if (bare == typeof(HeartbeatInformation))
            {
                var heartbeat = current as HeartbeatInformation;

                return new HeartbeatInformation
                {
                    LastReportAt = (DateTime)Different(typeof(DateTime), heartbeat?.LastReportAt)!,
                    ReportedStatus = heartbeat?.ReportedStatus ?? default
                };
            }

            throw new NotSupportedException(
                $"A rendered field of type {type} has no way to produce a different value, so this test cannot tell whether the version covers it.");
        }

        static IEnumerable<Type> Types(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type is not null)!;
            }
        }
    }
}
