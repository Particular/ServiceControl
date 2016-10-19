namespace ReleaseNotesCompiler
{
    using System.Linq;
    using Octokit;

    static class IssueExtensions
    {
        public static bool IsBug(this Issue issue)
        {
            return issue.Labels.Any(label => label.Name == "Bug");
        }

        public static bool IsImprovement(this Issue issue)
        {
            return issue.Labels.Any(label => label.Name == "Improvement");
        }

        public static bool IsFeature(this Issue issue)
        {
            return issue.Labels.Any(label => label.Name == "New Feature");
        }

        public static bool IsCritical(this Issue issue)
        {
            return issue.Labels.Any(label => label.Name == "Critical Bug");
        }
    }
}