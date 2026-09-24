namespace ServiceControl.UnitTests.Hosting
{
    using System;
    using NUnit.Framework;
    using Particular.ServiceControl.Hosting;
    using ServiceControl.Hosting.Commands;

    [TestFixture]
    class SetupCommandTests
    {
        [Test]
        public void Refuses_to_set_up_an_error_ingestion_only_worker()
        {
            var arguments = new HostArguments(["--setup", "--error-ingestion-only"]);

            // No settings, so a missing guard fails on a null reference instead of provisioning whatever this machine configures.
            var exception = Assert.ThrowsAsync<Exception>(() => new SetupCommand().Execute(arguments, settings: null));

            Assert.That(exception.Message, Does.Contain("--error-ingestion-only runs no setup"));
        }
    }
}
