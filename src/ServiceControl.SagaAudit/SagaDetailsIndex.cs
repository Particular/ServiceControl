namespace ServiceControl.SagaAudit
{
    using System.Linq;
    using Raven.Client.Indexes;

    public class SagaDetailsIndex : AbstractMultiMapIndexCreationTask<SagaHistory>
    {
        public SagaDetailsIndex()
        {
            AddMap<SagaSnapshot>(docs => from doc in docs
                select new
                {
                    doc.SagaId,
                    Id = doc.SagaId,
                    doc.SagaType,
                    Changes = new[]
                    {
                        new SagaStateChange
                        {
                            Endpoint = doc.Endpoint,
                            FinishTime = doc.FinishTime,
                            InitiatingMessage = doc.InitiatingMessage,
                            OutgoingMessages = doc.OutgoingMessages,
                            StartTime = doc.StartTime,
                            StateAfterChange = doc.StateAfterChange,
                            Status = doc.Status
                        }
                    }
                });

            //Legacy so we still scan old sagahistories
            AddMap<SagaHistory>(docs => from doc in docs
                select new
                {
                    doc.SagaId,
                    Id = doc.SagaId,
                    doc.SagaType,
                    doc.Changes
                }
            );

            Reduce = results => from result in results
                group result by result.SagaId
                into g
                let first = g.First()
                select new SagaHistory
                {
                    Id = first.SagaId,
                    SagaId = first.SagaId,
                    SagaType = first.SagaType,
                    Changes = g.SelectMany(x => x.Changes)
                        .OrderByDescending(x => x.FinishTime)
                        .ToList()
                };
        }
    }
}