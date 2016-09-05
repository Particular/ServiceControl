namespace HttpApiWrapper
{
    using System.Collections.Generic;
    using System.Linq;

    public static class ByteArrayExtensions
    {
        public static string ToHexString(this IEnumerable<byte> bytes)
        {
            return new string(bytes.SelectMany(x => x.ToString("X2").ToCharArray()).ToArray());
        }
    }
}
