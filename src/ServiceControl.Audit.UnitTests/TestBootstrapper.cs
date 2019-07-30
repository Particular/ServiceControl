namespace ServiceControl.Audit.UnitTests.API
{
    using LightInject;
    using LightInject.Nancy;

    class TestBootstrapper : LightInjectNancyBootstrapper
    {
        readonly IServiceContainer container;

        public TestBootstrapper(IServiceContainer container)
        {
            this.container = container;
        }
        protected override IServiceContainer GetServiceContainer()
        {
            return container;
        }
    }
}
