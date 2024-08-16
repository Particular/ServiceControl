namespace ServiceControl.UnitTests
{
    using System.Collections.Generic;
    using NUnit.Framework;

    public static class HeaderAssertions
    {
        public static void AssertHeader(this IDictionary<string, string> headers, string key, string expectedValue)
        {
            var result = headers.TryGetValue(key, out var value);
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True, $"Expected header [{key}] missing");
                Assert.That(value, Is.EqualTo(expectedValue), $"Header [{key}] has incorrect value\nExpected: {expectedValue}\nActual: {value}");
            });
        }

        public static void AssertHeaderMissing(this IDictionary<string, string> headers, string key)
        {
            Assert.That(headers.ContainsKey(key), Is.False, $"Unexpected header [{key}] found.");
        }
    }
}