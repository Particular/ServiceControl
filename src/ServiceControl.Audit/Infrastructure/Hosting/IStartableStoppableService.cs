using System;
using System.Threading.Tasks;

namespace ServiceControl.Audit.Infrastructure.Hosting
{
    interface IStartableStoppableService
    {
        Task Start();
        Action OnStopping { get; set; }
    }
}