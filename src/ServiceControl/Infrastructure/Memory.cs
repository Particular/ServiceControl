namespace ServiceControl.Infrastructure
{
    using Microsoft.IO;

    static class Memory
    {
        public static readonly RecyclableMemoryStreamManager Manager = new RecyclableMemoryStreamManager();
    }
}