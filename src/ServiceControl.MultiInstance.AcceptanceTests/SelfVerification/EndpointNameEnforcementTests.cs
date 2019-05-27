namespace ServiceBus.Management.AcceptanceTests.SelfVerification
{
    using System;
    using System.Linq;
    using System.Reflection;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;

    [TestFixture]
    class EndpointNameEnforcementTests : NServiceBusAcceptanceTest
    {
        const int endpointNameMaxLength = 60;

        [Test]
        public void EndpointName_should_not_exceed_maximum_length()
        {
            var testTypes = Assembly.GetExecutingAssembly().GetTypes()
                .Where(IsEndpointClass);

            var violators = testTypes
                .Where(t => Conventions.EndpointNamingConvention(t).Length > endpointNameMaxLength)
                .ToList();

            CollectionAssert.IsEmpty(violators, string.Join(",", violators));
        }

        static bool IsEndpointClass(Type t) => endpointConfigurationBuilderType.IsAssignableFrom(t);

        static Type endpointConfigurationBuilderType = typeof(EndpointConfigurationBuilder);
    }
}