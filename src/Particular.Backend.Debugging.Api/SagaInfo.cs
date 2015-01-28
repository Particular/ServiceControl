namespace Particular.Backend.Debugging.Api
{
    using System;

    public class SagaInfo
    {
        public string ChangeStatus { get; set; }
        public string SagaType { get; set; }
        public Guid SagaId { get; set; }
    }
}