using Microsoft.IO;

namespace ServiceControl.Audit.Infrastructure
{
    public static class Memory
    {
        public static readonly RecyclableMemoryStreamManager Manager = new RecyclableMemoryStreamManager();
    }
}