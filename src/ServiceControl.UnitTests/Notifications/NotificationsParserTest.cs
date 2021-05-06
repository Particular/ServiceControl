namespace ServiceControl.UnitTests.Notifications
{
    using NUnit.Framework;
    using ServiceControl.Notifications.Email;

    [TestFixture]
    public class NotificationsParserTest
    {
        [Test]
        public void ShouldSplitIdsOnDelimiter()
        {
            var ids = NotificationsFilterParser.Parse("id1#id2");

            CollectionAssert.AreEquivalent(new[] { "id1", "id2" }, ids);
        }

        [Test]
        public void ShouldRemoveEmptyEntries()
        {
            var ids = NotificationsFilterParser.Parse("#id1#id2#");

            CollectionAssert.AreEquivalent(new[] { "id1", "id2" }, ids);
        }

        [Test]
        public void ShouldSupportDelimiterEscaping()
        {
            var ids = NotificationsFilterParser.Parse("prefix##suffix#id2###");

            CollectionAssert.AreEquivalent(new[] { "prefix#suffix", "id2#" }, ids);
        }
    }
}