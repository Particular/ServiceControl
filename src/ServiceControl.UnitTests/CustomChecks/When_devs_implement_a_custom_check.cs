namespace ServiceControl.UnitTests.CustomChecks
{
    using System;
    using System.IO;
    using System.Linq;
    using Acme.ConnectivityChecks;
    using EndpointPlugin.CustomChecks;
    using EndpointPlugin.Messages.CustomChecks;
    using NUnit.Framework;

    public class When_devs_implement_a_custom_check
    {
        [Test]
        public void Should_default_the_category_to_namespace_minus_checks()
        {
            check.WhenEverIFeelLikeItIllMakeSureThatThisGetsInvoked();
            var messageObject = fakeBackend.MessagesSent.First();
            
            Assert.IsTrue(messageObject.GetType() == typeof(ReportCustomCheckResult), "The message sent to ServiceControl in this case must be of type ReportCustomCheckResult");
            Assert.AreEqual("Connectivity", ((ReportCustomCheckResult)messageObject).Category, "The category must be reported correctly");
        }

        [Test]
        public void Should_record_the_time_of_the_check()
        {
            check.WhenEverIFeelLikeItIllMakeSureThatThisGetsInvoked();
            var messageObject = fakeBackend.MessagesSent.First();

            Assert.IsTrue(messageObject.GetType() == typeof(ReportCustomCheckResult), "The message sent to ServiceControl in this case must be of type ReportCustomCheckResult");
            Assert.LessOrEqual(DateTime.UtcNow, ((ReportCustomCheckResult)messageObject).ReportedAt);
        }


        [Test]
        public void Should_generate_a_id_based_on_the_type()
        {
            check.WhenEverIFeelLikeItIllMakeSureThatThisGetsInvoked();

            var messageObject = fakeBackend.MessagesSent.First();
            Assert.IsTrue(messageObject.GetType() == typeof(ReportCustomCheckResult), "The message sent to ServiceControl in this case must be of type ReportCustomCheckResult");
            Assert.AreEqual(typeof(CustomResourceFolderCheck).FullName, ((ReportCustomCheckResult)messageObject).CustomCheckId);
        }

        [SetUp]
        public void SetUp()
        {
            fakeBackend = new FakeServiceControlBackend();
            check = new CustomResourceFolderCheck
            {
                ServiceControlBackend = fakeBackend
            };
        }

        CustomResourceFolderCheck check;
        FakeServiceControlBackend fakeBackend;

    }


    namespace Acme.ConnectivityChecks
    {
        public class CustomResourceFolderCheck : CustomCheck
        {
            public void WhenEverIFeelLikeItIllMakeSureThatThisGetsInvoked()
            {
                if (Directory.Exists("./resources"))
                {
                    ReportOk();
                }
                else
                {
                    ReportFailed("Resource directory not found");
                }
            }
        }
    }
}