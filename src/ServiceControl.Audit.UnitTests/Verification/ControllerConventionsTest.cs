namespace ServiceControl.Audit.UnitTests.Verification
{
    using System.Linq;
    using System.Reflection;
    using Microsoft.AspNetCore.Mvc;
    using NUnit.Framework;
    using ServiceControl.Audit.Infrastructure.Settings;

    [TestFixture]
    public class ControllerConventionsTest
    {
        [Test]
        public void All_controllers_should_match_convention()
        {
            var allControllers = typeof(Settings).Assembly.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t)).ToArray();
            Assert.IsNotEmpty(allControllers);
            Assert.That(allControllers.All(c => c.Name.EndsWith("Controller")), Is.True);
            Assert.That(allControllers.All(c => c.GetCustomAttributes<ApiControllerAttribute>().Any()), Is.True);
        }
    }
}