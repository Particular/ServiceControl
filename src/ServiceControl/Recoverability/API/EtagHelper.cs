using ServiceControl.Recoverability;
using System.Collections.Generic;
using System.Text;

static class EtagHelper
{
    public static string CalculateEtag(IEnumerable<GroupOperation> groups)
    {
        var data = new StringBuilder();
        foreach (var g in groups)
        {
            data.Append($"{g.Id}.{g.Count}.{g.OperationStatus}.{g.OperationProgress}.{g.OperationStartTime}.{g.OperationCompletionTime}.{g.NeedUserAcknowledgement}.{g.Comment}");
        }

        return data.ToString();
    }

    internal static string CalculateEtag(IList<FailureGroupView> results)
    {
        var data = new StringBuilder();
        foreach (var g in results)
        {
            data.Append($"{g.Id}.{g.Count}");
        }

        return data.ToString();
    }
}