namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System;
    using System.Threading;

    public static class Chunker
    {
        private const int CHUNK_SIZE = 500;

        public static void ExecuteInChunks(int total, Action<int, int, int> action, CancellationToken token)
        {
            if (total == 0)
            {
                return;
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            if (total < CHUNK_SIZE)
            {
                action(0, total - 1, total);
                return;
            }

            int start = 0, end = CHUNK_SIZE - 1;

            var totalSoFar = 0;
            do
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                totalSoFar += (end - start) + 1;
                action(start, end, totalSoFar);

                start = end + 1;
                end += CHUNK_SIZE;
                if (end > total)
                {
                    end = total - 1;
                }
            } while (start < total);
        }
    }
}