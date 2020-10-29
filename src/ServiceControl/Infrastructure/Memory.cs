namespace ServiceControl.Infrastructure
{
    using Microsoft.IO;

    public static class Memory
    {
        public static readonly RecyclableMemoryStreamManager Manager = new RecyclableMemoryStreamManager();
    }
}