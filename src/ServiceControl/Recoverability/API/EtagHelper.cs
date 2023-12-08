using System.Collections.Generic;
using System.Text;
using ServiceControl.Recoverability;

static class EtagHelper
{
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