namespace Particular.ThroughputCollector.Persistence.Tests;

using System;
using System.Threading.Tasks;
using NUnit.Framework;

[TestFixture]
abstract class PersistenceTestFixture
{
    public Action<PersistenceSettings> SetSettings = _ => { };

    [SetUp]
    public virtual Task Setup()
    {
        configuration = new PersistenceTestsConfiguration();

        return configuration.Configure(SetSettings);
    }

    [TearDown]
    public virtual Task Cleanup() => configuration?.Cleanup();

    protected string PersisterName => configuration.Name;

    protected IThroughputDataStore DataStore => configuration.ThroughputDataStore;

    protected PersistenceTestsConfiguration configuration;
}