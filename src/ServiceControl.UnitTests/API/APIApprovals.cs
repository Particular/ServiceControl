namespace ServiceControl.UnitTests.API
{
    using NUnit.Framework;
    using Particular.Approvals;
    using PublicApiGenerator;
    //using ServiceControl.Infrastructure.DomainEvents;
    //using ServiceControl.Infrastructure.SignalR;
    //using System.Linq;
    using System.Runtime.CompilerServices;

    [TestFixture]
    class APIApprovals
    {
        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Approve()
        {
            //var serviceControlAssembly = typeof(Particular.ServiceControl.Bootstrapper).Assembly;
            //var publicTypes = serviceControlAssembly.DefinedTypes.ToArray();//.Where(t => typeof(IUserInterfaceEvent).IsAssignableFrom(t)).ToArray();

            var publicApi = ApiGenerator.GeneratePublicApi(typeof(Particular.ServiceControl.Bootstrapper).Assembly);//, publicTypes, shouldIncludeAssemblyAttributes: false);
            Approver.Verify(publicApi);
        }

        //[Test]
        //[MethodImpl(MethodImplOptions.NoInlining)]
        //public void ApproveDomainEventTypes()
        //{
        //    var serviceControlAssembly = typeof(Particular.ServiceControl.Bootstrapper).Assembly;
        //    var domainEventTypes = serviceControlAssembly.DefinedTypes.Where(t => typeof(IDomainEvent).IsAssignableFrom(t)).ToArray();

        //    var publicApi = ApiGenerator.GeneratePublicApi(serviceControlAssembly, domainEventTypes, shouldIncludeAssemblyAttributes: false);
        //    Approver.Verify(publicApi);
        //}

        //[Test]
        //[MethodImpl(MethodImplOptions.NoInlining)]
        //public void ApproveMessageTypes()
        //{
        //    var serviceControlAssembly = typeof(Particular.ServiceControl.Bootstrapper).Assembly;
        //    var notDomainEventNorUserInterfaceEventTypes = serviceControlAssembly.DefinedTypes.Where(t =>
        //        t.Namespace != null
        //        && t.Namespace.StartsWith("ServiceControl.Contracts")
        //        && !typeof(IDomainEvent).IsAssignableFrom(t)
        //        && !typeof(IUserInterfaceEvent).IsAssignableFrom(t)
        //        ).ToArray();

        //    var publicApi = ApiGenerator.GeneratePublicApi(serviceControlAssembly, notDomainEventNorUserInterfaceEventTypes, shouldIncludeAssemblyAttributes: false);
        //    Approver.Verify(publicApi);
        //}
    }
}
