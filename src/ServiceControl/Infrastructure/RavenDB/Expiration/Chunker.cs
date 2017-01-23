namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System;

    public static class Chunker
    {
        const int CHUNK_SIZE = 500;

        public static void ExecuteInChunks(int total, Action<int, int> action)
        {
            if (total == 0)
            {
                return;
            }

            if (total < CHUNK_SIZE)
            {
                action(0, total - 1);
                return;
            }

            int start = 0, end = CHUNK_SIZE - 1;

            do
            {
                action(start, end);

                start = end + 1;
                end += CHUNK_SIZE;
                if (end >= total)
                {
                    end = total - 1;
                }
            } while (start < total);
        }
    }
}
