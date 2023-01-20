﻿namespace ServiceControl.Audit.Persistence.UnitOfWork
{
    public interface IAuditIngestionUnitOfWorkFactory
    {
        IAuditIngestionUnitOfWork StartNew(int batchSize); //Throws if not enough space or some other problem preventing from writing data
        bool CanIngestMore();
    }
}