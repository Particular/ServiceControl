using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ServiceControl.AcceptanceTesting;
using ServiceControl.AcceptanceTests;
using ServiceControl.MessageFailures.Api;

public static class FailedMessageExtensions
{
    internal static async Task<string> GetOnlyFailedUnresolvedMessageId(this AcceptanceTest test)
    {
        var allFailedMessages =
            await test.TryGet<IList<FailedMessageView>>($"/api/errors/?status=unresolved");
        if (!allFailedMessages.HasResult)
        {
            return null;
        }

        if (allFailedMessages.Item.Count != 1)
        {
            return null;
        }

        return allFailedMessages.Item.First().Id;
    }
}