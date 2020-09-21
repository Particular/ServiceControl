namespace ServiceControl.SagaAudit
{
    using System;
    using System.Collections.Generic;

    public class SagaHistory
    {
        public SagaHistory()
        {
            Changes = new List<SagaStateChange>();
        }

        public string Id { get; set; }
        public string SagaId { get; set; }
        public string SagaType { get; set; }
        public List<SagaStateChange> Changes { get; set; }
    }
}