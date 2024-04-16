namespace Particular.ThroughputCollector.Persistence.Tests;

using System.Threading.Tasks;
using NUnit.Framework;

[TestFixture]
abstract class PersistenceTestFixture
{
    [SetUp]
    public virtual Task Setup()
    {
        configuration = new PersistenceTestsConfiguration();

        return configuration.Configure();
    }

    [TearDown]
    public virtual Task Cleanup() => configuration?.Cleanup();

    protected string PersisterName => configuration.Name;

    protected IThroughputDataStore DataStore => configuration.ThroughputDataStore;

    protected PersistenceTestsConfiguration configuration;
}