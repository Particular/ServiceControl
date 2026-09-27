namespace ServiceControl.Audit.UnitTests.Infrastructure
{
    using System;
    using System.Runtime.Loader;
    using System.Threading.Tasks;
    using Audit.Infrastructure.Hosting;
    using Audit.Infrastructure.Hosting.Commands;
    using Audit.Infrastructure.Settings;
    using NUnit.Framework;

    class MaintenanceModeTests
    {
        [Test]
        public void Should_refuse_unsupported_persister_before_starting_host()
        {
            var settings = new Settings(persisterType: "InMemory")
            {
                AssemblyLoadContextResolver = static _ => AssemblyLoadContext.Default
            };

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await new MaintenanceModeCommand().Execute(new HostArguments([]), settings));

            Assert.That(exception.Message, Does.Contain("Maintenance mode is not supported").And.Contain("InMemory"));
        }
    }
}
