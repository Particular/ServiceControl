namespace ReleaseNotesCompiler.Tests
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;
    using Octokit;
    using IGitHubClient = ReleaseNotesCompiler.IGitHubClient;

    public class FakeGitHubClient : IGitHubClient
    {
        public List<Milestone> Milestones { get; set; }
        public List<Issue> Issues { get; set; }
        public int NumberOfCommits { get; set; }

        public FakeGitHubClient()
        {
            Milestones = new List<Milestone>();
            Issues = new List<Issue>();
        }

        public Task<int> GetNumberOfCommitsBetween(Milestone previousMilestone, Milestone currentMilestone)
        {
            return Task.FromResult(NumberOfCommits);
        }

        public Task<List<Issue>> GetIssues(Milestone targetMilestone)
        {
            return Task.FromResult(Issues);
        }

        public Task<IReadOnlyList<Milestone>> GetMilestones()
        {
            return Task.FromResult<IReadOnlyList<Milestone>>(new ReadOnlyCollection<Milestone>(Milestones));
        }
    }
}