namespace ServiceControlInstaller.CustomActions.UnitTests
{
    using NUnit.Framework;

    [TestFixture]
    public class RemoveOldVersionTests
    {
        [Test, Explicit]
        public void RunCleanup()
        {
            CustomActionsMigrations.RemoveProductFromMSIList(new TestLogger());
        }
    }
}