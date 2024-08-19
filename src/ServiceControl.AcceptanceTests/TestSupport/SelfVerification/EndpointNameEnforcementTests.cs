﻿namespace ServiceControl.AcceptanceTests.TestSupport.SelfVerification
{
    using System;
    using System.Linq;
    using System.Reflection;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NUnit.Framework;

    [TestFixture]
    class EndpointNameEnforcementTests : NServiceBusAcceptanceTest
    {
        [Test]
        public void EndpointName_should_not_exceed_maximum_length()
        {
            var testTypes = Assembly.GetExecutingAssembly().GetTypes()
                .Where(IsEndpointClass);

            var violators = testTypes
                .Where(t => Conventions.EndpointNamingConvention(t).Length > endpointNameMaxLength)
                .ToList();

            Assert.That(violators, Is.Empty, string.Join(",", violators));
        }

        static bool IsEndpointClass(Type t) => endpointConfigurationBuilderType.IsAssignableFrom(t);
        const int endpointNameMaxLength = 60;

        static readonly Type endpointConfigurationBuilderType = typeof(EndpointConfigurationBuilder);
    }
}