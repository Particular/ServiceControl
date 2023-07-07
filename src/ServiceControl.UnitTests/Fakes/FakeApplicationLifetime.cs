namespace ServiceControl.UnitTests
{
    using System;
    using System.Threading;
    using Microsoft.Extensions.Hosting;

    class FakeApplicationLifetime : IHostApplicationLifetime
    {
        public void StopApplication() => throw new NotImplementedException();

        public CancellationToken ApplicationStarted { get; } = new CancellationToken();
        public CancellationToken ApplicationStopping { get; } = new CancellationToken();
        public CancellationToken ApplicationStopped { get; } = new CancellationToken();
    }
}
