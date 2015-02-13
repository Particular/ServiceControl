namespace ServiceControl.SagaAudit
{
    using System;
    using System.Linq;
    using Raven.Client.Indexes;

    public class SagaListIndex : AbstractMultiMapIndexCreationTask<SagaListIndex.Result>
    {
        public class Result
        {
            public Guid Id;
            public string Uri;
            public string SagaType;
        }

        public SagaListIndex()
        {
            AddMap<SagaSnapshot>(docs => from doc in docs
                select new Result
                       {
                           Id = doc.SagaId,
                           Uri = "api/sagas/" + doc.SagaId,
                           SagaType = doc.SagaType,
                       });
            AddMap<SagaHistory>(docs => from doc in docs
                select new Result
                       {
                           Id = doc.SagaId,
                           Uri = "api/sagas/" + doc.SagaId,
                           SagaType = doc.SagaType,
                       });

            Reduce = results => from result in results
                group result by result.Id
                into g
                let first = g.First()
                select new Result
                       {
                           Id = g.Key,
                           Uri = first.Uri,
                           SagaType = first.SagaType
                       };
        }
    }
}