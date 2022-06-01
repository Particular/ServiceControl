namespace ServiceControl.Audit.UnitTests.Verification
{
    using System.Linq;
    using System.Web.Http.Controllers;
    using System.Web.Http.Dispatcher;
    using Audit.Auditing;
    using NUnit.Framework;

    [TestFixture]
    public class ControllerConventionsTest
    {
        [Test]
        public void All_controllers_should_match_convention()
        {
            var allControllers = typeof(FailedAuditImport).Assembly.GetTypes().Where(t => typeof(IHttpController).IsAssignableFrom(t)).ToArray();
            Assert.IsNotEmpty(allControllers);
            Assert.IsTrue(allControllers.All(c => c.Name.EndsWith(DefaultHttpControllerSelector.ControllerSuffix)));
        }
    }
}