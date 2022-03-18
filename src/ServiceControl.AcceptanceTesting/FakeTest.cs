namespace ServiceControl.AcceptanceTesting
{
    using NUnit.Framework;

    // As this assembly references Microsoft.NET.Test.Sdk, it needs to have at least one test
    // or not having any passed tests will be interpreted as failure.
    [TestFixture]
    class FakeTest
    {
        [Test]
        public void IsTrue()
        {
            Assert.True(true);
        }
    }
}