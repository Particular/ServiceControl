using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Octokit;

namespace ReleaseNotesCompiler
{
    using static System.String;

    public class ReleaseNotesBuilder
    {
        public ReleaseNotesBuilder(IGitHubClient gitHubClient, string user, string repository, string milestoneTitle)
        {
            this.gitHubClient = gitHubClient;
            this.user = user;
            this.repository = repository;
            this.milestoneTitle = milestoneTitle;
        }

        public async Task<string> BuildReleaseNotes()
        {
            var milestones = await gitHubClient.GetMilestones();

            var targetMilestone = milestones.FirstOrDefault(x => x.Title == milestoneTitle);

            if (targetMilestone == null)
            {
                throw new Exception($"Could not find milestone for '{milestoneTitle}'.");
            }
            var issues = await gitHubClient.GetIssues(targetMilestone);
            var stringBuilder = new StringBuilder();
            var previousMilestone = GetPreviousMilestone(targetMilestone, milestones);
            var numberOfCommits = await gitHubClient.GetNumberOfCommitsBetween(previousMilestone, targetMilestone);

            if (issues.Count > 0)
            {
                var issuesText = Format(issues.Count == 1 ? "{0} issue" : "{0} issues", issues.Count);

                if (numberOfCommits > 0)
                {
                    var commitsLink = GetCommitsLink(targetMilestone, previousMilestone);
                    var commitsText = Format(numberOfCommits == 1 ? "{0} commit" : "{0} commits", numberOfCommits);

                    stringBuilder.Append($"As part of this release we had [{commitsText}]({commitsLink}) which resulted in [{issuesText}]({targetMilestone.HtmlUrl()}) being closed.");
                }
                else
                {
                    stringBuilder.Append($"As part of this release we had [{issuesText}]({targetMilestone.HtmlUrl()}) closed.");
                }
            }
            else if (numberOfCommits > 0)
            {
                var commitsLink = GetCommitsLink(targetMilestone, previousMilestone);
                var commitsText = Format(numberOfCommits == 1 ? "{0} commit" : "{0} commits", numberOfCommits);
                stringBuilder.Append($"As part of this release we had [{commitsText}]({commitsLink}).");
            }
            stringBuilder.AppendLine();

            stringBuilder.AppendLine(targetMilestone.Description);
            stringBuilder.AppendLine();

            AddIssues(stringBuilder, issues);

            AddFooter(stringBuilder);

            return stringBuilder.ToString();
        }

        Milestone GetPreviousMilestone(Milestone targetMilestone, IReadOnlyList<Milestone> milestones)
        {
            var currentVersion = targetMilestone.Version();
            return milestones
                .OrderByDescending(m => m.Version())
                .Distinct().ToList()
                .SkipWhile(x => x.Version() >= currentVersion)
                .FirstOrDefault();
        }

        string GetCommitsLink(Milestone targetMilestone, Milestone previousMilestone)
        {
            if (previousMilestone == null)
            {
                return $"https://github.com/{user}/{repository}/commits/{targetMilestone.Title}";
            }
            return $"https://github.com/{user}/{repository}/compare/{previousMilestone.Title}...{targetMilestone.Title}";
        }

        void AddIssues(StringBuilder builder, List<Issue> issues)
        {
            var critical = issues
               .Where(issue => issue.IsCritical())
               .ToList();

            if (critical.Any())
            {
                PrintCriticalBanner(builder);
                PrintHeading("Critical Fixes", builder);

                PrintIssue(builder, critical, true);

                builder.AppendLine();
            }

            var features = issues.Where(issue => issue.IsFeature())
                         .ToList();

            if (features.Any())
            {
                PrintHeading("New Features", builder);

                PrintIssue(builder, features, true);

                builder.AppendLine();
            }

            var improvements = issues.Where(issue => issue.IsImprovement())
                         .ToList();

            if (improvements.Any())
            {
                PrintHeading("Improvements", builder);

                PrintIssue(builder, improvements);

                builder.AppendLine();
            }

            var bugs = issues
               .Where(issue => issue.IsBug())
               .ToList();

            if (bugs.Any())
            {
                PrintHeading("Bug Fixes", builder);

                PrintIssue(builder, bugs);

                builder.AppendLine();
            }
        }

        static void AddFooter(StringBuilder stringBuilder)
        {
            stringBuilder.AppendLine("## Where to get it");
            stringBuilder.AppendLine("You can download this release from [website](http://particular.net/downloads).");
        }

        static void PrintHeading(string heading, StringBuilder builder)
        {
            builder.AppendLine($"## {heading}");
        }

        static void PrintCriticalBanner(StringBuilder builder)
        {
            builder.AppendLine("### Upgrade Immediately");
            builder.AppendLine("All users are advised to upgrade to this version of ServiceControl immediately.");
        }

        static void PrintIssue(StringBuilder builder, List<Issue> relevantIssues, bool printBody = false)
        {
            foreach (var issue in relevantIssues)
            {
                if (printBody)
                {
                    builder.AppendLine($"- **[{issue.Number}]({issue.HtmlUrl}) - {issue.Title}**  ");
                    var issueBodyLines = issue.Body.Split(new [] { Environment.NewLine}, StringSplitOptions.None);
                    foreach (var line in issueBodyLines)
                    {
                        if (line.StartsWith("--"))
                        {
                            break;
                        }
                        builder.AppendLine($"    {line}");
                    }
                    builder.AppendLine();
                }
                else
                {
                    builder.AppendLine($"- [**{issue.Number}**]({issue.HtmlUrl}) - {issue.Title}  ");
                }
            }
        }

        IGitHubClient gitHubClient;
        string user;
        string repository;
        string milestoneTitle;
    }
}
