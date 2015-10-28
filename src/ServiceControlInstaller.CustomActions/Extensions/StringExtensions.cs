namespace ServiceControlInstaller.CustomActions.Extensions
{
    using System.Collections.Generic;
    using System.Linq;

    static class StringExtensions
    {
        public static string Reverse(this string input)
        {
            return new string(input.ToCharArray().Reverse().ToArray());
        }

        public static IEnumerable<string> Chunks(this string str, int chunkSize)
        {
            return Enumerable.Range(0, str.Length / chunkSize)
                .Select(i => str.Substring(i * chunkSize, chunkSize));
        }
    }
}