﻿namespace ServiceControl.Audit.Persistence.RavenDb.Infrastructure
{
    using Microsoft.IO;

    static class Memory
    {
        public static readonly RecyclableMemoryStreamManager Manager = new RecyclableMemoryStreamManager();
    }
}