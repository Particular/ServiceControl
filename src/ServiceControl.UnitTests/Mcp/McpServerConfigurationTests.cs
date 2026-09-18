#nullable enable
namespace ServiceControl.UnitTests.Mcp;

using NUnit.Framework;
using ServiceControl.Mcp;

[TestFixture]
class McpServerConfigurationTests
{
    [Test]
    public void Server_instructions_guide_the_model_toward_the_important_tools()
    {
        Assert.Multiple(() =>
        {
            Assert.That(McpServerConfiguration.ServerInstructions, Does.Contain("get_errors_summary"));
            Assert.That(McpServerConfiguration.ServerInstructions, Does.Contain("get_failure_groups"));
            Assert.That(McpServerConfiguration.ServerInstructions, Does.Contain("Retry tools"));
        });
    }

    [Test]
    public void Overview_prompt_matches_the_expected_first_steps()
    {
        var prompt = ServiceControlMcpPrompts.ServiceControlOverview();

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("get_errors_summary"));
            Assert.That(prompt, Does.Contain("get_failed_message_by_id"));
            Assert.That(prompt, Does.Contain("retry_failure_group"));
        });
    }
}