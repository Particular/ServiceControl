using System.Collections.Generic;
using System.Text;

public static class EtagHelper
{
    public static string CalculateEtag(IEnumerable<GroupOperation> groups)
    {
        var data = new StringBuilder();
        foreach (var g in groups)
        {
            data.Append($"{g.Id}.{g.Count}.{g.OperationStatus}.{g.OperationProgress}.{g.OperationStartTime}.{g.OperationCompletionTime}.{g.NeedUserAcknowledgement}");
        }

        return data.ToString();
    }
}