﻿namespace ServiceControl.UnitTests.API
{
    using NUnit.Framework;
    using Particular.Approvals;
    using PublicApiGenerator;
    using ServiceControl.Infrastructure.SignalR;
    using System.Linq;
    using System.Runtime.CompilerServices;

    [TestFixture]
    class APIApprovals
    {
        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void ApproveUserInterfaceEventTypes()
        {
            var serviceControlAssembly = typeof(Particular.ServiceControl.Bootstrapper).Assembly;
            var userInterfaceEventTypes = serviceControlAssembly.DefinedTypes.Where(t => typeof(IUserInterfaceEvent).IsAssignableFrom(t)).ToArray();

            var publicApi = ApiGenerator.GeneratePublicApi(serviceControlAssembly, userInterfaceEventTypes, shouldIncludeAssemblyAttributes: false);
            Approver.Verify(publicApi);
        }
    }
}
