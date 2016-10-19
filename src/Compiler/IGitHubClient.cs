namespace ReleaseNotesCompiler
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Octokit;

    public interface IGitHubClient
    {
        Task<int> GetNumberOfCommitsBetween(Milestone previousMilestone, Milestone currentMilestone);
        Task<List<Issue>> GetIssues(Milestone targetMilestone);
        Task<IReadOnlyList<Milestone>> GetMilestones();
    }
}