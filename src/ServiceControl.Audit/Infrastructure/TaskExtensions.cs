﻿namespace ServiceControl.Audit.Infrastructure
{
    using System.Threading.Tasks;

    static class TaskEx
    {
        // Used to explicitly suppress the compiler warning about
        // using the returned value from async operations
        public static void Ignore(this Task task)
        {
        }
    }
}