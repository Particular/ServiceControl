using System;
using System.Threading.Tasks;

namespace ServiceControl.Hosting
{
    interface IStartableStoppableService
    {
        Task Start();
        Action OnStopping { get; set; }
    }
}