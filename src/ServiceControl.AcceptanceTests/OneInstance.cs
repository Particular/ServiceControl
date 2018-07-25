using System.Threading.Tasks;
using NUnit.Framework;
using ServiceBus.Management.AcceptanceTests;

[SetUpFixture]
public class OneInstance
{    
    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await AcceptanceTest.Stop();
    }
}