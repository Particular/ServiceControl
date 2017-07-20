namespace ServiceControl.Config.Extensions
{
    using System;

    static class StringExtensions
    {
        public static string TruncateSentence(this string value, int maxLength)
        {
            var sentence = value.Split('.')[0];
            return string.IsNullOrEmpty(sentence) ? sentence : sentence.Substring(0, Math.Min(sentence.Length, maxLength)) + "...";
        }
    }
}