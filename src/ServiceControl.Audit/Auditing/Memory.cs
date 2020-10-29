namespace ServiceControl.Audit.Auditing
{
    using Microsoft.IO;

    public static class Memory
    {
        public static readonly RecyclableMemoryStreamManager Manager = new RecyclableMemoryStreamManager();
    }
}