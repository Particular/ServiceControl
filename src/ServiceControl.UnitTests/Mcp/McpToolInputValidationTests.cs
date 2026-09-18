#nullable enable
namespace ServiceControl.UnitTests.Mcp;

using System;
using NUnit.Framework;
using ServiceControl.Mcp;

[TestFixture]
class McpToolInputValidationTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("undefined")]
    [TestCase("UNDEFINED")]
    public void NormalizeOptionalFilter_treats_blank_and_undefined_as_missing(string? value)
    {
        Assert.That(McpToolInputValidation.NormalizeOptionalFilter(value), Is.Null);
    }

    [TestCase(null, "time_of_failure")]
    [TestCase("time_sent", "time_sent")]
    [TestCase("MESSAGE_TYPE", "message_type")]
    public void NormalizeSort_accepts_the_allowed_values_and_defaults_when_missing(string? value, string expected)
    {
        Assert.That(McpToolInputValidation.NormalizeSort(value), Is.EqualTo(expected));
    }

    [TestCase(null, "desc")]
    [TestCase("asc", "asc")]
    [TestCase("DESC", "desc")]
    public void NormalizeDirection_accepts_the_allowed_values_and_defaults_when_missing(string? value, string expected)
    {
        Assert.That(McpToolInputValidation.NormalizeDirection(value), Is.EqualTo(expected));
    }

    [TestCase(null)]
    [TestCase("undefined")]
    [TestCase("UNDEFINED")]
    [TestCase("resolved")]
    [TestCase("ARCHIVED")]
    public void NormalizeStatus_accepts_the_allowed_values_and_omits_missing_values(string? value)
    {
        var expected = McpToolInputValidation.NormalizeOptionalFilter(value) is null ? null : value!.Trim().ToLowerInvariant();

        Assert.That(McpToolInputValidation.NormalizeStatus(value), Is.EqualTo(expected));
    }

    [Test]
    public void NormalizeSort_rejects_invalid_values()
    {
        Assert.That(() => McpToolInputValidation.NormalizeSort("not-a-sort"), Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void NormalizeDirection_rejects_invalid_values()
    {
        Assert.That(() => McpToolInputValidation.NormalizeDirection("up"), Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void NormalizeStatus_rejects_invalid_values()
    {
        Assert.That(() => McpToolInputValidation.NormalizeStatus("bad-status"), Throws.TypeOf<ArgumentException>());
    }
}