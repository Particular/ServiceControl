﻿namespace ServiceControl.Persistence
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IPersistenceLifecycle
    {
        Task Start(CancellationToken cancellationToken = default);
        Task Stop(CancellationToken cancellationToken = default);
    }
}
