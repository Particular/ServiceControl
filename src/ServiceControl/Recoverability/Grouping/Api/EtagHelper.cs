using System.Collections.Generic;
using System.Text;

public static class EtagHelper
{
    public static string CalculateEtag(IEnumerable<RetryGroup> groups)
    {
        var data = new StringBuilder();
        foreach (var g in groups)
        {
            data.Append($"{g.Id}.{g.Count}.{g.RetryStatus}.{g.RetryProgress}.{g.RetryStartTime}.{g.RetryCompletionTime}.{g.NeedUserAcknowledgement}");
        }
        return data.ToString();
    }
}