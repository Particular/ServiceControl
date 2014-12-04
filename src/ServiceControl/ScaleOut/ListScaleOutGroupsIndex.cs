namespace ServiceControl.ScaleOut
{
    using System.Linq;
    using Raven.Client.Indexes;
    using ServiceControl.MessageFailures.Api;

    public class ListScaleOutGroupsIndex : AbstractIndexCreationTask<ScaleOutGroupRegistration, ListScaleOutGroupsIndex.Result>
    {
        public ListScaleOutGroupsIndex()
        {
            Map = messages => from message in messages
                select new
                {
                    message.GroupId,
                };

            Reduce = messages => from message in messages
                group message by message.GroupId
                into g
                select new
                {
                    GroupId = g.Key,
                };

            DisableInMemoryIndexing = true;
        }

        public class Result
        {
            public string GroupId { get; set; }
        }
    }
}