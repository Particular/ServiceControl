namespace ServiceControl.UnitTests.ScatterGather
{
    using System.Net.Http;
    using CompositeViews.Messages;
    using NUnit.Framework;

    [TestFixture]
    public class RemoteInstanceEtagTests
    {
        [TestCase("\"4611686018427387904\"", TestName = "A_remote_etag_is_read_when_the_instance_quotes_it")]
        [TestCase("4611686018427387904", TestName = "A_remote_etag_is_read_when_the_instance_predates_the_conditional_get_fix")]
        public void A_remote_etag_is_read(string asSentByTheRemoteInstance)
        {
            var response = new HttpResponseMessage();
            response.Headers.TryAddWithoutValidation("ETag", asSentByTheRemoteInstance);

            Assert.That(ScatterGatherApiBase.ReadEtag(response.Headers), Is.EqualTo("4611686018427387904"),
                "a rolling upgrade runs both shapes side by side, so both have to be understood");
        }

        [Test]
        public void An_instance_that_sends_no_etag_contributes_nothing()
        {
            var response = new HttpResponseMessage();

            Assert.That(ScatterGatherApiBase.ReadEtag(response.Headers), Is.Null);
        }
    }
}
