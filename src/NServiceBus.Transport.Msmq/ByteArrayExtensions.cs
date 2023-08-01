using System;

static class ByteArrayExtensions
{
    public static int LastIndexOf(this byte[] data, byte[] pattern)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        if (pattern == null)
        {
            throw new ArgumentNullException(nameof(pattern));
        }

        if (pattern.Length > data.Length)
        {
            return -1;
        }

        var cycles = data.Length - pattern.Length + 1;
        for (var dataIndex = cycles; dataIndex > 0; dataIndex--)
        {
            if (data[dataIndex] != pattern[0])
            {
                continue;
            }

            int patternIndex;
            for (patternIndex = pattern.Length - 1; patternIndex >= 1; patternIndex--)
            {
                if (data[dataIndex + patternIndex] != pattern[patternIndex])
                {
                    break;
                }
            }

            if (patternIndex == 0)
            {
                return dataIndex;
            }
        }
        return -1;
    }
}