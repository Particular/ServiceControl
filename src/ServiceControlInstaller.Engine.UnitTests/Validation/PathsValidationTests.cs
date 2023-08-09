﻿namespace ServiceControlInstaller.Engine.UnitTests.Validation
{
    using System.IO;
    using System.Linq;
    using Engine.Validation;
    using Instances;
    using NUnit.Framework;

    [TestFixture]
    public class PathsValidationTests
    {
        class FakeServiceControlPaths : IServiceControlPaths
        {
            public string DBPath { get; set; }

            public string InstallPath { get; set; }

            public string LogPath { get; set; }
        }

        //[SetUp]
        //public void Init()
        //{
        //    var instanceA = new FakeServiceControlPaths
        //    {
        //        InstallPath = @"c:\test\1\bin",
        //        LogPath = @"c:\test\1\logs",
        //        DBPath = @"c:\test\1\db"
        //    };
        //}

        [Test]
        public void CheckPathsAreUnique_ShouldThrow()
        {
            var newInstance = new ServiceControlNewInstance
            {
                InstallPath = @"c:\test\1\bin",
                LogPath = @"c:\test\1\bin",
                DBPath = @"c:\test\1\bin"
            };

            var p = new PathsValidator(newInstance);

            var ex = Assert.Throws<EngineValidationException>(() => p.CheckPathsAreUnique());
            Assert.That(ex.Message, Is.EqualTo("The installation path, log path and database path must be unique"));
        }

        [Test]
        public void CheckPathsAreUnique_ShouldSucceed()
        {
            var newInstance = new ServiceControlNewInstance
            {
                InstallPath = @"c:\test\1\bin",
                LogPath = @"c:\test\1\log",
                DBPath = @"c:\test\1\db"
            };

            var p = new PathsValidator(newInstance);
            Assert.DoesNotThrow(() => p.CheckPathsAreUnique());
        }


        [Test]
        public void CheckPathsAreValid_ShouldSucceed()
        {
            var newInstance = new ServiceControlNewInstance
            {
                InstallPath = @"c:\test\1\bin",
                LogPath = @"c:\test\1\bin",
                DBPath = @"c:\test\1\bin"
            };

            var p = new PathsValidator(newInstance);
            Assert.DoesNotThrow(() => p.CheckPathsAreValid());
        }

        [Test]
        public void CheckPathsAreValid_ShouldThrow()
        {
            //Invalid path
            var p = new PathsValidator(new ServiceControlNewInstance { InstallPath = @"?>c:\test\1\bin" });

            var ex = Assert.Throws<EngineValidationException>(() => p.CheckPathsAreValid());
            Assert.That(ex.Message, Is.EqualTo("The install path is set to an invalid path"));

            //Partial path
            p = new PathsValidator(new ServiceControlNewInstance
            {
                InstallPath = @"\test\1\bin"
            });

            ex = Assert.Throws<EngineValidationException>(() => p.CheckPathsAreValid());
            Assert.That(ex.Message, Is.EqualTo("The install path is set to an invalid path"));

            //No Drive
            p = new PathsValidator(new ServiceControlNewInstance { InstallPath = $@"{GetAnUnsedDriveLetter()}:\test\1\bin" });
            ex = Assert.Throws<EngineValidationException>(() => p.CheckPathsAreValid());
            Assert.That(ex.Message, Is.EqualTo("The install path does not go to a supported drive"));
        }

        [Test]
        public void CheckNoNestedPaths_ShouldThrow()
        {
            var newInstance = new ServiceControlNewInstance
            {
                InstallPath = @"c:\test\1",
                LogPath = @"c:\test\1\log",
                DBPath = @"c:\test\1\db"
            };

            var p = new PathsValidator(newInstance);
            var ex = Assert.Throws<EngineValidationException>(() => p.CheckNoNestedPaths());
            Assert.That(ex.Message, Does.Contain("Nested paths are not supported"));
        }

        [Test]
        public void CheckNoNestedSiblingPaths_ShouldSucceed()
        {
            var newInstance = new ServiceControlNewInstance
            {
                InstallPath = @"c:\test\1\servicecontrol",
                LogPath = @"c:\test\1\servicecontrollog",
                DBPath = @"c:\test\1\servicecontroldb"
            };

            var p = new PathsValidator(newInstance);
            Assert.DoesNotThrow(() => p.CheckNoNestedPaths());
        }

        [Test]
        public void CheckNoNestedPaths_ShouldSucceed()
        {
            var newInstance = new ServiceControlNewInstance
            {
                InstallPath = @"c:\test\1\bin",
                LogPath = @"c:\test\1\log",
                DBPath = @"c:\test\1\db"
            };

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