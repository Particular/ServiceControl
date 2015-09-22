using System.Linq;
using Raven.Client.Indexes;

namespace Issue558Detector
{
    public class MessageHistories : AbstractIndexCreationTask<EventLogItem, MessageHistories.Result>
    {
        public class Result
        {
            public string MessageId { get; set; }
            public TimelineEntry[] Events { get; set; }
        }

        public MessageHistories()
        {
            Map = docs => from doc in docs
                from message in doc.RelatedTo.Where(x => x.StartsWith("/message/"))
                select new
                {
                    MessageId = message,
                    Events = new[] { 
                        new
                        {
                            Id = doc.Id, 
                            When = doc.RaisedAt, 
                            Event = doc.EventType
                        }
                    }
                };

            Reduce = results => from result in results
                group result by result.MessageId into g
                select new
                {
                    MessageId = g.Key,
                    Events = g.SelectMany(x => x.Events).OrderBy(x => x.When).ToArray()
                };
        }
    }
}