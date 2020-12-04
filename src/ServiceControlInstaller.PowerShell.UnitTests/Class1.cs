using System.Collections.Generic;
using System.IO;

namespace ServiceControlInstaller.PowerShell.UnitTests
{
    using ServiceControlInstaller.PowerShell;
    using System.Diagnostics;
    using NUnit;
    using NUnit.Framework;
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;
    using System.Collections.ObjectModel;
    using System.Security;


    [TestFixture]
    public class NewServiceControlInstanceTests
    {
        readonly string tmpPath = Path.GetTempPath().TrimEnd('\\');
        Runspace runSpace;
        RunspaceConfiguration config;

        Pipeline pipe;
        Command command;


        [SetUp]
        public void PSSetup()
        {
            config = RunspaceConfiguration.Create();

            //PSSnapInException warning;
            //config.AddPSSnapIn("LuifITPSSnapin", out warning);

            //runSpace = RunspaceFactory.CreateRunspace();
            runSpace = RunspaceFactory.CreateRunspace(config);
            runSpace.Open();
            //var cmd = runSpace.CreatePipeline($"set-Location '{tmpPath}'");
            //cmd.Invoke();


            pipe = runSpace.CreatePipeline();
            command = new Command("New-ServiceControlInstance");
            pipe.Commands.Add(command);
        }

        [TearDown]
        public void FixtureTearDown()
        {
            runSpace.Close();
        }

        [Test]
        public void XX()
        {
            //Arrange
            command.Parameters.Add(new CommandParameter("Person", "ME"));

            //Act
            Collection<PSObject> psObject = pipe.Invoke();

            //Assert
            Assert.AreEqual(1, psObject.Count);
            Assert.IsTrue((bool) psObject[0].BaseObject);
        }


        [Test]
        public void PSTest()
        {
            var cmd = runSpace.CreatePipeline("get-location");
            var resultObject = cmd.Invoke();
            var currDir = resultObject[0].ToString();
            Assert.AreEqual(tmpPath, currDir);

            cmd = runSpace.CreatePipeline(@".\new-securestring.ps1 password");
            resultObject = cmd.Invoke();
            var ss = (SecureString) resultObject[0].ImmediateBaseObject;
            Assert.AreEqual(0, ss.Length, nameof(ss.Length));

            runSpace.SessionStateProxy.SetVariable("ss", ss);
            cmd = runSpace.CreatePipeline(@".\getfrom-securestring.ps1 $ss");
            resultObject = cmd.Invoke();
            var clearText = (string) resultObject[0].ImmediateBaseObject;
            Assert.AreEqual("password", clearText, "password");
        }

        // New-ServiceControlInstance -Name Test -Transport MSMQ -InstallPath c:\install -DBPath c:\db -LogPath c:\logs -Port 12 -AuditRetentionPeriod "15.0:0:0" -ErrorRetentionPeriod "15.0:0:0" -ForwardAuditMessages -DatabaseMaintenancePort 2 -ErrorVariable MyError

        [Explicit]
        [Test]
        public void IsMsmqInstalled()
        {
            var cmdlet = new NewServiceControlInstance();
            var results = cmdlet.Invoke();
            foreach (var result in results)
            {
                Debug.WriteLine(result);
            }
        }

        [Test]
        public void XXXX()
        {
            var parameters = new Dictionary<string, object>
            {
                {"Name", "Test"},
                {"Transport", "MSMQ"},
                {"InstallPath", @"c:\install"},
                {"DBPath", @"c:\db"},
                {"LogPath", @"c:\logs"},
                {"Port", "12"},
                //{"AuditRetentionPeriod", "15.0:0:0"},
                //{"ForwardAuditMessages", true},
                {"ErrorRetentionPeriod", "15.0:0:0"},
                {"DatabaseMaintenancePort", "2"},
            };
            
            var results = new PsCmdletAssert().Invoke(typeof(NewServiceControlInstance), parameters);
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("expected", results[0]);
        }
    }
}