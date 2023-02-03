namespace ServiceControl.Transport.Tests
{
    using System;
    using System.Threading.Tasks;

    public class TransportTestsConfiguration
    {
        public TransportTestsConfiguration()
        {
        }

        public Task Cleanup() => throw new NotImplementedException();
        public Task Configure() => throw new NotImplementedException();
    }
}