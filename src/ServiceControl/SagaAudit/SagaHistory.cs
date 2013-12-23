namespace ServiceControl.SagaAudit
{
    using System;
    using System.Collections.Generic;
    using Infrastructure;

    public class SagaHistory
    {
        public SagaHistory()
        {
            Changes = new List<SagaStateChange>();
        }

        public string Id { get; set; }
        public string Type { get; set; }
        public List<SagaStateChange> Changes { get; set; }
        public Guid SagaId { get; set; }

        public static Guid MakeId(string endPointName, Guid sagaId)
        {
            return DeterministicGuid.MakeId(endPointName, sagaId.ToString());
        }
    }
}