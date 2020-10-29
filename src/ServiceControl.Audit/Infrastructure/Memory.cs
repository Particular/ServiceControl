namespace ServiceControl.Audit.Infrastructure
{
    using Microsoft.IO;
    
    public static class Memory
    {
        public static readonly RecyclableMemoryStreamManager Manager = new RecyclableMemoryStreamManager();
    }
}