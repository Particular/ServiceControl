namespace ServiceControlInstaller.CustomActions.UnitTests
{
    using NUnit.Framework;

    [TestFixture]
    public class RemoveOldVersionTests
    {
        [Test]
        [Explicit]
        public void RunCleanup()
        {
            CustomActionsMigrations.RemoveProductFromMSIList(new TestLogger());
        }

        [Test]
        public void NoOp()
        {
            // dotnet test fails if no non-explicit tests are found in a test project
            Assert.IsTrue(true);
        }
    }
}