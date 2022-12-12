﻿namespace ServiceControl.Infrastructure.RavenDB
{
    using System;
    using System.Threading;

    public static class Chunker
    {
        public static int ExecuteInChunks<T1, T2>(int total, Func<T1, T2, int, int, int> action, T1 t1, T2 t2, CancellationToken cancellationToken = default)
        {
            if (total == 0)
            {
                return 0;
            }

            if (total < CHUNK_SIZE)
            {
                return action(t1, t2, 0, total - 1);
            }

            int start = 0, end = CHUNK_SIZE - 1;
            var chunkCount = 0;
            do
            {
                chunkCount += action(t1, t2, start, end);

                start = end + 1;
                end += CHUNK_SIZE;
                if (end >= total)
                {
                    end = total - 1;
                }
            }
            while (start < total && !cancellationToken.IsCancellationRequested);

            return chunkCount;
        }

        const int CHUNK_SIZE = 500;
    }
}