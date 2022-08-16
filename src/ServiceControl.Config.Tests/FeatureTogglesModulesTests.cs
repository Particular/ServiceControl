namespace ServiceControl.Config.Tests
{
    using Autofac;
    using Framework.Modules;
    using NUnit.Framework;

    [TestFixture]
    public class FeatureTogglesModulesTests
    {
        [Test]
        public void FeatureTogglePropertiesAreInjected()
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule(new FeatureTogglesModule());
            builder.RegisterType<FakeClass>();

            var container = builder.Build();

            var featureToggles = container.Resolve<FeatureToggles>();
            featureToggles.Enable("SomeFeature");

            var injectionTarget = container.Resolve<FakeClass>();

            Assert.IsTrue(injectionTarget.SomeFeatureIsEnabled, "Property with activated feature toggle should be set.");
            Assert.IsFalse(injectionTarget.SomeUnrelatedFeatureIsEnabled, "Property without activated feature toggle should be ignored.");
        }

        class FakeClass
        {
            [FeatureToggle("SomeFeature")]
            public bool SomeFeatureIsEnabled { get; set; }

            [FeatureToggle("UnrelatedFeature")]
            public bool SomeUnrelatedFeatureIsEnabled { get; set; }
        }
    }
}