﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octokit;

namespace ReleaseNotesCompiler
{
    public static class OctokitExtensions
    {
        public static bool IsPullRequest(this Issue issue)
        {
            return issue.PullRequest != null;
        }

        public static async Task<IEnumerable<Issue>> AllIssuesForMilestone(this GitHubClient gitHubClient, Milestone milestone)
        {
            var closedIssueRequest = new RepositoryIssueRequest
            {
                Milestone = milestone.Number.ToString(),
                State = ItemStateFilter.Closed
            };
            var openIssueRequest = new RepositoryIssueRequest
            {
                Milestone = milestone.Number.ToString(),
                State = ItemStateFilter.Open
            };
            var parts = milestone.Url.AbsolutePath.Split('/');
            var user = parts[2];
            var repository = parts[3];
            var closedIssues = await gitHubClient.Issue.GetAllForRepository(user, repository, closedIssueRequest);
            var openIssues = await gitHubClient.Issue.GetAllForRepository(user, repository, openIssueRequest);
            return openIssues.Union(closedIssues);
        }

        public static string HtmlUrl(this  Milestone milestone)
        {
            var parts = milestone.Url.AbsolutePath.Split('/');
            var user = parts[2];
            var repository = parts[3];
            return string.Format("https://github.com/{0}/{1}/issues?milestone={2}&state=closed", user, repository, milestone.Number);
        }

        static IEnumerable<string> FixHeaders(IEnumerable<string> lines)
        {
            var inCode = false;
            foreach (var line in lines)
            {
                if (line.StartsWith("```"))
                {
                    inCode = !inCode;
                }
                if (!inCode && line.StartsWith("#"))
                {
                    yield return "###" + line;
                }
                else
                {
                    yield return line;
                }
            }
        }
    }
}