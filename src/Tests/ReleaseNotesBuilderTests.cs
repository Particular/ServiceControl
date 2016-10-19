﻿namespace ReleaseNotesCompiler.Tests
{
    using System;
    using System.Linq;
    using ApprovalTests;
    using NUnit.Framework;
    using Octokit;

    [TestFixture]
    public class ReleaseNotesBuilderTests
    {
        [Test]
        public void NoCommitsNoIssues()
        {
            AcceptTest(0);
        }

        [Test]
        public void NoCommitsSomeIssues()
        {
            AcceptTest(0, CreateIssue(1, "Bug"), CreateIssue(2, "Feature"), CreateIssue(3, "Improvement"));
        }

        [Test]
        public void SomeCommitsNoIssues()
        {
            AcceptTest(5);
        }

        [Test]
        public void SomeCommitsSomeIssues()
        {
            AcceptTest(5, CreateIssue(1, "Bug"), CreateIssue(2, "Feature"), CreateIssue(3, "Improvement"));
        }

        [Test]
        public void SingularCommitsNoIssues()
        {
            AcceptTest(1);
        }

        [Test]
        public void SingularCommitsSomeIssues()
        {
            AcceptTest(1, CreateIssue(1, "Bug"), CreateIssue(2, "Feature"), CreateIssue(3, "Improvement"));
        }

        [Test]
        public void SingularCommitsSingularIssues()
        {
            AcceptTest(1, CreateIssue(1, "Bug"));
        }

        [Test]
        public void NoCommitsSingularIssues()
        {
            AcceptTest(0, CreateIssue(1, "Bug"));
        }

        [Test]
        public void SomeCommitsSingularIssues()
        {
            AcceptTest(5, CreateIssue(1, "Bug"));
        }

        static void AcceptTest(int commits, params Issue[] issues)
        {
            var fakeClient = new FakeGitHubClient();

            fakeClient.Milestones.Add(CreateMilestone("1.2.3"));

            fakeClient.NumberOfCommits = commits;

            foreach (var issue in issues)
            {
                fakeClient.Issues.Add(issue);
            }

            var builder = new ReleaseNotesBuilder(fakeClient, "TestUser", "FakeRepo", "1.2.3");

            var notes = builder.BuildReleaseNotes().Result;

            Approvals.Verify(notes);
        }


        static Milestone CreateMilestone(string version)
        {
            return new Milestone(new Uri("https://github.com/Particular/FakeRepo/issues?q=milestone%3A" + version), 0, ItemState.Open, version, String.Empty, null, 0, 0, DateTimeOffset.Now, null, null);
        }

        static Issue CreateIssue(int number, params string[] labels)
        {
            return new Issue(null,
                new Uri("http://example.com/" + number),
                null,
                null,
                number,
                ItemState.Open,
                "Issue " + number,
                "Some issue",
                null,
                null,
                labels.Select(x => new Label(null, x, null)).ToArray(),
                null,
                null,
                0,
                null,
                null,
                DateTimeOffset.Now,
                null,
                1,
                false,
                null);
        }
    }
}