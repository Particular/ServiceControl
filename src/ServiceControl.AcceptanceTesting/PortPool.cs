namespace ServiceControl.AcceptanceTesting
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using TestHelper;

    public class PortPool
    {
        readonly int startingPort;
        readonly HashSet<int> inUse;

        public PortPool(int startingPort)
        {
            this.startingPort = startingPort;
            inUse = [];
        }

        public PortLease GetLease() => new PortLease(this);

        internal int LeasePort()
        {
            lock (inUse)
            {
                var start = inUse.Any() ? inUse.Max() + 1 : startingPort;
                var port = PortUtility.FindAvailablePort(start);
                inUse.Add(port);
                TestContext.Out.WriteLine($"Port leased: {port}");
                return port;
            }
        }

        internal void Return(int port)
        {
            lock (inUse)
            {
                inUse.Remove(port);
            }
        }
    }

    public class PortLease : IDisposable
    {
        readonly PortPool owner;
        readonly List<int> leasedPorts;

        internal PortLease(PortPool owner)
        {
            this.owner = owner;
            leasedPorts = [];
        }

        public int GetPort()
        {
            var port = owner.LeasePort();
            leasedPorts.Add(port);
            return port;
        }

        public void Dispose()
        {
            foreach (var port in leasedPorts)
            {
                owner.Return(port);
            }
        }
    }
}
