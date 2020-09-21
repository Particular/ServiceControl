using Microsoft.IO;

namespace ServiceControl.Audit.Infrastructure
{
    static class Memory
    {
        public static readonly RecyclableMemoryStreamManager Manager = new RecyclableMemoryStreamManager();
    }
}
