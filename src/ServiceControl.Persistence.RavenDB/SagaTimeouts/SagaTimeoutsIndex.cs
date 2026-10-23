namespace ServiceControl.Persistence.RavenDB.SagaTimeouts
{
    using System;
    using System.Linq;
    using Raven.Client.Documents.Indexes;
    using ServiceControl.Persistence.SagaTimeouts;

    class SagaTimeoutsIndex : AbstractIndexCreationTask<SagaTimeout, SagaTimeoutsIndex.Result>
    {
        public SagaTimeoutsIndex()
        {
            Map = timeouts =>
                from timeout in timeouts
                select new Result
                {
                    Status = timeout.Status,
                    DueTime = timeout.DueTime,
                    EndpointName = timeout.EndpointName,
                    SagaType = timeout.SagaType,
                    SagaId = timeout.SagaId,
                    CorrelationValue = timeout.CorrelationValue,
                    TimeoutType = timeout.TimeoutType,
                    LastChangedAt = timeout.LastChangedAt,
                    Query = new[] { timeout.CorrelationValue, timeout.SagaType, timeout.TimeoutType, timeout.EndpointName }
                };

            Index(x => x.Query, FieldIndexing.Search);
        }

        public class Result
        {
            public SagaTimeoutStatus Status { get; set; }
            public DateTime? DueTime { get; set; }
            public string EndpointName { get; set; }
            public string SagaType { get; set; }
            public Guid SagaId { get; set; }
            public string CorrelationValue { get; set; }
            public string TimeoutType { get; set; }
            public DateTime? LastChangedAt { get; set; }
            public string[] Query { get; set; }
        }
    }
}
