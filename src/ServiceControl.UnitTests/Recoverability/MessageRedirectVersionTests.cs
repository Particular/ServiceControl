namespace ServiceControl.UnitTests.Operations
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using ServiceControl.Persistence.MessageRedirects;

    [TestFixture]
    public class MessageRedirectVersionTests
    {
        [Test]
        public void From_address_changed_should_change_version()
        {
            var knownVersion = ResponseVersions.VersionOf(Redirects(Redirect(from: "old@machine")));

            var moved = ResponseVersions.VersionOf(Redirects(Redirect(from: "new@machine")));

            Assert.That(moved.Matches(knownVersion), Is.False);
        }

        [Test]
        public void To_address_changed_should_change_version()
        {
            var redirect = Redirect(to: "old@machine");
            var data = Redirects(redirect);

            var knownVersion = ResponseVersions.VersionOf(data);

            redirect.ToPhysicalAddress = "new@machine";

            Assert.That(ResponseVersions.VersionOf(data).Matches(knownVersion), Is.False);
        }

        [Test]
        public void Last_modified_changed_should_change_version()
        {
            var redirect = Redirect();
            var data = Redirects(redirect);

            var knownVersion = ResponseVersions.VersionOf(data);

            redirect.LastModified = redirect.LastModified.AddTicks(1);

            Assert.That(ResponseVersions.VersionOf(data).Matches(knownVersion), Is.False);
        }

        [Test]
        public void Changing_item_count_should_change_version()
        {
            var emptyVersion = ResponseVersions.VersionOf(Redirects());

            var oneRedirect = Redirects(Redirect());

            Assert.That(ResponseVersions.VersionOf(oneRedirect).Matches(emptyVersion), Is.False,
                "an empty list is a representation like any other, so this compares two real versions rather than a version against nothing");
        }

        [Test]
        public void An_empty_list_still_reports_a_version()
        {
            var version = ResponseVersions.VersionOf(Redirects());

            Assert.That(version.HasValue, Is.True,
                "a client watching a list that stays empty must be able to revalidate it rather than refetch the emptiness");
        }

        static IReadOnlyList<MessageRedirect> Redirects(params MessageRedirect[] redirects) => redirects;

        static MessageRedirect Redirect(string from = "sales@machine", string to = "sales@other") =>
            new()
            {
                FromPhysicalAddress = from,
                ToPhysicalAddress = to,
                LastModified = new DateTime(2026, 8, 19, 10, 0, 0, DateTimeKind.Utc)
            };
    }
}
