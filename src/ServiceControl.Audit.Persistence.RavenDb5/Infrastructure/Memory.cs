namespace ServiceControl.Audit.Persistence.RavenDb5.Infrastructure
{
    using Microsoft.IO;

    static class Memory
    {
        public static readonly RecyclableMemoryStreamManager Manager = new RecyclableMemoryStreamManager();
    }
}