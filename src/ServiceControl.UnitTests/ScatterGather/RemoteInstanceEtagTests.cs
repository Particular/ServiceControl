namespace ServiceControl.UnitTests.ScatterGather
{
    using System.Net.Http;
    using CompositeViews.Messages;
    using NUnit.Framework;
    using ServiceControl.Persistence.Infrastructure;

    [TestFixture]
    public class RemoteInstanceEtagTests
    {
        [TestCase("W/\"4611686018427387904\"", TestName = "A_remote_etag_is_read_when_the_instance_marks_it_weak")]
        [TestCase("\"4611686018427387904\"", TestName = "A_remote_etag_is_read_when_the_instance_quotes_it")]
        [TestCase("4611686018427387904", TestName = "A_remote_etag_is_read_when_the_instance_predates_the_conditional_get_fix")]
        public void A_remote_etag_is_read(string asSentByTheRemoteInstance)
        {
            var response = new HttpResponseMessage();
            response.Headers.TryAddWithoutValidation("ETag", asSentByTheRemoteInstance);

            Assert.That(ScatterGatherApiBase.ReadEtag(response.Headers).ToString(), Is.EqualTo("4611686018427387904"),
                "a rolling upgrade runs all three shapes side by side, so all three have to be understood");
        }

        [Test]
        public void An_instance_that_sends_no_etag_contributes_nothing()
        {
            var response = new HttpResponseMessage();

            Assert.That(ScatterGatherApiBase.ReadEtag(response.Headers).HasValue, Is.False);
        }
    }
}
