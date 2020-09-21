using Microsoft.IO;

namespace ServiceControl.Infrastructure
{
    static class Memory
    {
        public static readonly RecyclableMemoryStreamManager Manager = new RecyclableMemoryStreamManager();
    }
}
