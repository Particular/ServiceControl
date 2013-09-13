namespace ServiceControl.UnitTests.CustomChecks
{
    using System;
    using System.IO;
    using System.Linq;
    using Acme.ConnectivityChecks;
    using EndpointPlugin.CustomChecks;
    using NUnit.Framework;

    public class When_devs_implement_a_custom_check
    {
        [Test]
        public void Should_default_the_category_to_namespace_minus_checks()
        {
            check.WhenEverIFeelLikeItIllMakeSureThatThisGetsInvoked();

            Assert.AreEqual("Connectivity", fakeBackend.ReportedChecks.First().Category);
        }

        [Test]
        public void Should_record_the_time_of_the_check()
        {
            check.WhenEverIFeelLikeItIllMakeSureThatThisGetsInvoked();

            Assert.LessOrEqual(DateTime.UtcNow, fakeBackend.ReportedChecks.First().ReportedAt);
        }


        [Test]
        public void Should_generate_a_id_based_on_the_type()
        {
            check.WhenEverIFeelLikeItIllMakeSureThatThisGetsInvoked();

            Assert.AreEqual(typeof(CustomResourceFolderCheck).FullName, fakeBackend.ReportedChecks.First().CustomCheckId);
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
        public class PeriodicResourceFolderCheck : PeriodicCustomCheck
        {
            protected override TimeSpan Interval
            {
                get { return TimeSpan.FromMinutes(15); }
            }

            public override void PerformCheck()
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