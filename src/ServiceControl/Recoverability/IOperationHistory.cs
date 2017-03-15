namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;

    public interface IOperationHistory<T>
    {
        List<T> HistoricOperations { get; set; }
    }
}
