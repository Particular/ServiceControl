namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Linq;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using Particular.Approvals;

    class CustomCheckTests : PersistenceTestFixture
    {
        [Test]
        public void VerifyCustomChecks() =>
            // HINT: Custom checks are documented on the docs site and Id and Category are published in integration events
            // If any changes have been made to custom checks, this may break customer integration subscribers.
            Approver.Verify(
                string.Join(Environment.NewLine,
                    from check in ServiceProvider.GetServices<ICustomCheck>()
                    orderby check.Category, check.Id
                    select $"{check.Category}: {check.Id}"
                )
            );
    }
}