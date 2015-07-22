namespace ServiceControl.Recoverability.Groups.Indexes
{
    using System.Collections.Generic;
    using System.Linq;
    using Raven.Client.Indexes;
    using ServiceControl.MessageFailures;

    public class MessageFailuresByGrouperSetIndex : AbstractIndexCreationTask<MessageFailureHistory, MessageFailuresByGroupSet>
    {
        public MessageFailuresByGrouperSetIndex()
        {
            Map = failures => from failure in failures
                where failure.Status == FailedMessageStatus.Unresolved
                let grouperSetId = failure.FailureGroups == null ? string.Empty :string.Join(";", failure.FailureGroups.OrderBy(f => f.Type).Select(i => i.Type))
                select new MessageFailuresByGroupSet
                {
                    MessageIds = new List<string>{failure.UniqueMessageId},
                    GrouperSetId = grouperSetId
                };

            Reduce = results => from result in results
                group result by result.GrouperSetId
                into g
                select new MessageFailuresByGroupSet
                {
                    MessageIds = g.SelectMany(x => x.MessageIds).ToList(),
                    GrouperSetId = g.Key
                };
        }
    }

    public class MessageFailuresByGroupSet
    {
        public string GrouperSetId { get; set; }
        public List<string> MessageIds { get; set; }
    }
}