using System.Collections.Generic;
using System.Text;
using ServiceControl.Persistence.MessageRedirects;
using ServiceControl.Recoverability;

static class EtagHelper
{
    internal static string CalculateEtag(IReadOnlyList<MessageRedirect> redirects)
    {
        if (redirects.Count == 0)
        {
            return string.Empty;
        }

        var data = new StringBuilder();
        foreach (var redirect in redirects)
        {
            data.Append($"{redirect.MessageRedirectId}.{redirect.ToPhysicalAddress}.{redirect.LastModified.Ticks}");
        }

        return data.ToString();
    }

    public static string CalculateEtag(GroupOperation[] groups)
    {
        if (groups.Length == 0)
        {
            return string.Empty;
        }

        var data = new StringBuilder();
        foreach (var g in groups)
        {
            data.Append($"{g.Id}.{g.Count}.{g.OperationStatus}.{g.OperationProgress}.{g.OperationStartTime}.{g.OperationCompletionTime}.{g.NeedUserAcknowledgement}.{g.Comment}");
        }

        return data.ToString();
    }

    internal static string CalculateEtag(IList<FailureGroupView> results)
    {
        if (results.Count == 0)
        {
            return string.Empty;
        }

        var data = new StringBuilder();
        foreach (var g in results)
        {
            data.Append($"{g.Id}.{g.Count}");
        }

        return data.ToString();
    }
}