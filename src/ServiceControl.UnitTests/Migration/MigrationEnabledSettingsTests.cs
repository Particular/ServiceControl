namespace ServiceControl.UnitTests.Migration;

using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Configuration;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
[NonParallelizable]
class MigrationOptionsSettingsTests
{

    [Test]
    public void Only_MigrationEngineOptions_parses_the_engine_settings_keys()
    {
        var parsers = typeof(Settings).Assembly.GetTypes()
            .Concat(typeof(MigrationEngineOptions).Assembly.GetTypes())
            .Where(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Any(method => method.Name is "FromSettings" or "Read" or "Resolve"
                    && method.GetParameters() is [{ ParameterType.Name: nameof(SettingsRootNamespace) }, ..]))
            .Select(type => type.Name)
            .ToArray();

        Assert.That(parsers, Is.EquivalentTo(new[] { nameof(MigrationEngineOptions) }),
            "Migration/ThrottlePauseMilliseconds and its neighbours have exactly one parser. A second one drifts its defaults and its refusal message away from this one, and nothing fails until a customer types a window wrong.");
    }
}
