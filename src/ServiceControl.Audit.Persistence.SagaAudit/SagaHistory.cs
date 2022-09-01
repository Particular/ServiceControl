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

        public Guid Id { get; set; }
        public Guid SagaId { get; set; }
        public string SagaType { get; set; }
        public List<SagaStateChange> Changes { get; set; }
    }
}