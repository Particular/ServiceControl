namespace ServiceControl.Audit.Persistence.EFCore.EntityConfigurations;

using System.Security.Cryptography;
using System.Text;

static class ColumnLengths
{
    public const int ShortTextLength = 450;

    public static string FitToIndex(string value)
    {
        if (value.Length <= ShortTextLength)
        {
            return value;
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        var prefixLength = ShortTextLength - hash.Length - 1;

        if (char.IsHighSurrogate(value[prefixLength - 1]))
        {
            prefixLength--;
        }

        return $"{value[..prefixLength]}~{hash}";
    }
}
