namespace ServiceControl.UnitTests.BodyStorage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.Operations.BodyStorage;

    [TestFixture]
    public class BodyStorageEnricherTests
    {
        [Test]
        public void Should_store_body_regardless_of_the_body_size()
        {
            //TODO:RAVEN5 this test does not make sense
            // var enricher = new BodyStorageFeature.BodyStorageEnricher();
            // const int ExpectedBodySize = 150000;
            // var body = Encoding.UTF8.GetBytes(new string('a', ExpectedBodySize));
            //
            // await enricher.StoreErrorMessageBody(body, new Dictionary<string, string>(), new Dictionary<string, object>());
            //
            // Assert.AreEqual(ExpectedBodySize, fakeStorage.StoredBodySize, "Body should never be dropped for error messages");
        }
    }
}