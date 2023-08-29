namespace ServiceControlInstaller.Engine.UnitTests.Validation
{
    using System.IO;
    using System.Linq;
    using Engine.Validation;
    using Instances;
    using NUnit.Framework;

    [TestFixture]
    public class PathsValidationTests
    {
        [Test]
        public void CheckPathsAreUnique_ShouldThrow()
        {
            var newInstance = ServiceControlNewInstance.CreateWithDefaultPersistence();

            newInstance.InstallPath = @"c:\test\1\bin";
            newInstance.LogPath = @"c:\test\1\bin";
            newInstance.DBPath = @"c:\test\1\bin";

            var p = new PathsValidator(newInstance);

            var ex = Assert.Throws<EngineValidationException>(() => p.CheckPathsAreUnique());
            Assert.That(ex.Message, Is.EqualTo("The installation path, log path and database path must be unique"));
        }

        [Test]
        public void CheckPathsAreUnique_ShouldSucceed()
        {
            var newInstance = ServiceControlNewInstance.CreateWithDefaultPersistence();

            newInstance.InstallPath = @"c:\test\1\bin";
            newInstance.LogPath = @"c:\test\1\log";
            newInstance.DBPath = @"c:\test\1\db";

            var p = new PathsValidator(newInstance);
            Assert.DoesNotThrow(() => p.CheckPathsAreUnique());
        }


        [Test]
        public void CheckPathsAreValid_ShouldSucceed()
        {
            var newInstance = ServiceControlNewInstance.CreateWithDefaultPersistence();

            newInstance.InstallPath = @"c:\test\1\bin";
            newInstance.LogPath = @"c:\test\1\bin";
            newInstance.DBPath = @"c:\test\1\bin";

            var p = new PathsValidator(newInstance);
            Assert.DoesNotThrow(() => p.CheckPathsAreValid());
        }

        [Test]
        public void CheckPathsAreValid_ShouldThrow()
        {
            //Invalid path
            var instance = ServiceControlNewInstance.CreateWithDefaultPersistence();
            instance.InstallPath = @"?>c:\test\1\bin";
            var p = new PathsValidator(instance);

            var ex = Assert.Throws<EngineValidationException>(() => p.CheckPathsAreValid());
            Assert.That(ex.Message, Is.EqualTo("The install path is set to an invalid path"));

            instance.InstallPath = @"\test\1\bin";

            //Partial path
            p = new PathsValidator(instance);

            ex = Assert.Throws<EngineValidationException>(() => p.CheckPathsAreValid());
            Assert.That(ex.Message, Is.EqualTo("The install path is set to an invalid path"));

            //No Drive
            instance.InstallPath = $@"{GetAnUnsedDriveLetter()}:\test\1\bin";
            p = new PathsValidator(instance);
            ex = Assert.Throws<EngineValidationException>(() => p.CheckPathsAreValid());
            Assert.That(ex.Message, Is.EqualTo("The install path does not go to a supported drive"));
        }

        [Test]
        public void CheckNoNestedPaths_ShouldThrow()
        {
            var newInstance = ServiceControlNewInstance.CreateWithDefaultPersistence();

            newInstance.InstallPath = @"c:\test\1";
            newInstance.LogPath = @"c:\test\1\log";
            newInstance.DBPath = @"c:\test\1\db";

            var p = new PathsValidator(newInstance);
            var ex = Assert.Throws<EngineValidationException>(() => p.CheckNoNestedPaths());
            Assert.That(ex.Message, Does.Contain("Nested paths are not supported"));
        }

        [Test]
        public void CheckNoNestedSiblingPaths_ShouldSucceed()
        {
            var newInstance = ServiceControlNewInstance.CreateWithDefaultPersistence();

            newInstance.InstallPath = @"c:\test\1\servicecontrol";
            newInstance.LogPath = @"c:\test\1\servicecontrollog";
            newInstance.DBPath = @"c:\test\1\servicecontroldb";

            var p = new PathsValidator(newInstance);
            Assert.DoesNotThrow(() => p.CheckNoNestedPaths());
        }

        [Test]
        public void CheckNoNestedPaths_ShouldSucceed()
        {
            var newInstance = ServiceControlNewInstance.CreateWithDefaultPersistence();

            newInstance.InstallPath = @"c:\test\1\bin";
            newInstance.LogPath = @"c:\test\1\log";
            newInstance.DBPath = @"c:\test\1\db";

            var p = new PathsValidator(newInstance);

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