namespace ReleaseNotesCompiler
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Octokit;

    public class DefaultGitHubClient : IGitHubClient
    {
        GitHubClient gitHubClient;
        string user;
        string repository;

        public DefaultGitHubClient(GitHubClient gitHubClient, string user, string repository)
        {
            this.gitHubClient = gitHubClient;
            this.user = user;
            this.repository = repository;
        }

        public async Task<int> GetNumberOfCommitsBetween(Milestone previousMilestone, Milestone currentMilestone)
        {
            try
            {
                if (previousMilestone == null)
                {
                    var gitHubClientRepositoryCommitsCompare = await gitHubClient.Repository.Commit.Compare(user, repository, "master", currentMilestone.Title);
                    return gitHubClientRepositoryCommitsCompare.AheadBy;
                }

                var compareResult = await gitHubClient.Repository.Commit.Compare(user, repository, previousMilestone.Title, "master");
                return compareResult.AheadBy;
            }
            catch (NotFoundException)
            {
                //If there is not tag yet the Compare will return a NotFoundException
                //we can safely ignore
                return 0;
            }
        }

        public async Task<List<Issue>> GetIssues(Milestone targetMilestone)
        {
            var allIssues = await gitHubClient.AllIssuesForMilestone(targetMilestone);
            return allIssues.Where(x => x.State == ItemState.Closed).ToList();
        }

        public Task<IReadOnlyList<Milestone>> GetMilestones()
        {
            var milestonesClient = gitHubClient.Issue.Milestone;
            return milestonesClient.GetAllForRepository(user, repository, new MilestoneRequest
            {
                State = ItemStateFilter.All
            });
           
        }
    }
}