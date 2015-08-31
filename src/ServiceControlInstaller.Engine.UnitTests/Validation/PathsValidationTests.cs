namespace ServiceControlInstaller.Engine.UnitTests.Validation
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Moq;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Validation;

    [TestFixture]
    public class PathsValidationTests
    {
        List<IContainInstancePaths> instances;

        [SetUp]
        public void Init()
        {
            var instanceA = new Mock<IContainInstancePaths>();
            instanceA.SetupGet(p => p.InstallPath).Returns(@"c:\test\1\bin");
            instanceA.SetupGet(p => p.LogPath).Returns(@"c:\test\1\logs");
            instanceA.SetupGet(p => p.DBPath).Returns(@"c:\test\1\db");

            instances = new List<IContainInstancePaths>
            {
                instanceA.Object
            };
        } 

        [Test]
        public void CheckPathsAreUnique_ShouldThrow()
        {
            var newInstance = new ServiceControlInstanceMetadata
            {
                InstallPath = @"c:\test\1\bin",
                LogPath = @"c:\test\1\bin",
                DBPath = @"c:\test\1\bin"
            };

            var p = new PathsValidator(newInstance)
            {
                instances = instances
            };
            var ex = Assert.Throws<EngineValidationException>(() => p.CheckPathsAreUnique());
            Assert.That(ex.Message, Is.EqualTo("The installation path, log path and database path must be unique"));
        }

        [Test]
        public void CheckPathsAreUnique_ShouldSucceed()
        {
            var newInstance = new ServiceControlInstanceMetadata
            {
                InstallPath = @"c:\test\1\bin",
                LogPath = @"c:\test\1\log",
                DBPath = @"c:\test\1\db"
            };

            var p = new PathsValidator(newInstance)
            {
                instances = instances
            };
            Assert.DoesNotThrow(() => p.CheckPathsAreUnique());
        }

        [Test]
        public void CheckPathsNotUsedInOtherInstances_ShouldThrow()
        {
            var newInstance = new ServiceControlInstanceMetadata
            {
                InstallPath = @"c:\test\1\bin",  //This one is bad
                LogPath = @"c:\test\2\logs",
                DBPath = @"c:\test\2\db"
            };

            var p = new PathsValidator(newInstance)
            {
                instances = instances
            };
            var ex = Assert.Throws<EngineValidationException>(() => p.CheckPathsNotUsedInOtherInstances());
            Assert.That(ex.Message, Is.EqualTo("The install path specified is already assigned to another instance"));
        }

        [Test]
        public void CheckPathsNotUsedInOtherInstances_ShouldSuceed()
        {
            var newInstance = new ServiceControlInstanceMetadata
            {
                InstallPath = @"c:\test\2\bin",  
                LogPath = @"c:\test\2\logs",
                DBPath = @"c:\test\2\db"
            };

            var p = new PathsValidator(newInstance)
            {
                instances = instances
            };
            Assert.DoesNotThrow(() => p.CheckPathsNotUsedInOtherInstances());

        }

        [Test]
        public void CheckPathsAreValid_ShouldSucceed()
        {
            var newInstance = new ServiceControlInstanceMetadata
            {
                InstallPath = @"c:\test\1\bin",
                LogPath = @"c:\test\1\bin",
                DBPath = @"c:\test\1\bin"
            };

            var p = new PathsValidator(newInstance)
            {
                instances = instances
            };
   
            Assert.DoesNotThrow(() => p.CheckPathsAreValid());
        }

        [Test]
        public void CheckPathsAreValid_ShouldThrow()
        {
            //Invalid path
            var p = new PathsValidator(new ServiceControlInstanceMetadata { InstallPath = @"?>c:\test\1\bin" } )
            {
                instances = instances
            };
            var ex = Assert.Throws<EngineValidationException>(() => p.CheckPathsAreValid());
            Assert.That(ex.Message, Is.EqualTo("The install path is set to an invalid path"));

            //Partial path
            p = new PathsValidator(new ServiceControlInstanceMetadata { InstallPath = @"\test\1\bin" })
            {
                instances = instances
            };

            ex = Assert.Throws<EngineValidationException>(() => p.CheckPathsAreValid());
            Assert.That(ex.Message, Is.EqualTo("The install path is set to an invalid path"));

            //No Drive
            p = new PathsValidator(new ServiceControlInstanceMetadata { InstallPath = string.Format( @"{0}:\test\1\bin", GetAnUnsedDriveLetter()) })
            {
                instances = instances
            };
            ex = Assert.Throws<EngineValidationException>(() => p.CheckPathsAreValid());
            Assert.That(ex.Message, Is.EqualTo("The install path does not go to a supported drive"));
        }

        [Test]
        public void CheckNoNestedPaths_ShouldThrow()
        {
            var newInstance = new ServiceControlInstanceMetadata
            {
                InstallPath = @"c:\test\1",
                LogPath = @"c:\test\1\log",
                DBPath = @"c:\test\1\db"
            };

            var p = new PathsValidator(newInstance)
            {
                instances = instances
            };
            var ex = Assert.Throws<EngineValidationException>(() => p.CheckNoNestedPaths());
            Assert.That(ex.Message, Is.StringContaining("Nested paths are not supported"));
        }

        [Test]
        public void CheckNoNestedPaths_ShouldSucceed()
        {
            var newInstance = new ServiceControlInstanceMetadata
            {
                InstallPath = @"c:\test\1\bin",
                LogPath = @"c:\test\1\log",
                DBPath = @"c:\test\1\db"
            };

            var p = new PathsValidator(newInstance)
            {
                instances = instances
            };
            Assert.DoesNotThrow(() => p.CheckNoNestedPaths());
        }

        char GetAnUnsedDriveLetter()
        {
            var letters = "CDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray().SelectMany(q => q.ToString()).ToArray();
            var driveletters = DriveInfo.GetDrives().SelectMany(q => q.Name[0].ToString().ToUpper()).ToArray();
            return letters.Except(driveletters).First();
        }

    }
}
