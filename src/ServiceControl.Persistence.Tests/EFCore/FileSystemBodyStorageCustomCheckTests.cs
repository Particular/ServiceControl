namespace ServiceControl.Persistence.Tests;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NServiceBus.CustomChecks;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.Implementation.BodyStorage;

[TestFixture]
class FileSystemBodyStorageCustomCheckTests : BasePersistence
{
    const long TotalSize = 1000;

    [Test]
    public async Task Passes_when_free_space_is_above_the_threshold()
    {
        var check = CreateCheck(availableFreeSpace: 200, threshold: 15);

        var result = await check.PerformCheck();

        Assert.That(result.HasFailed, Is.False, result.FailureReason);
    }

    [Test]
    public async Task Fails_when_free_space_is_exactly_at_the_threshold()
    {
        var check = CreateCheck(availableFreeSpace: 150, threshold: 15);

        var result = await check.PerformCheck();

        Assert.That(result.HasFailed, Is.True);
    }

    [Test]
    public async Task Fails_when_free_space_is_below_the_threshold()
    {
        var check = CreateCheck(availableFreeSpace: 100, threshold: 15);

        var result = await check.PerformCheck();

        Assert.That(result.HasFailed, Is.True);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.FailureReason, Does.Contain(StoragePath));
            Assert.That(result.FailureReason, Does.Contain(Environment.MachineName));
        }
    }

    [Test]
    public async Task Honours_a_configured_threshold()
    {
        var check = CreateCheck(availableFreeSpace: 200, threshold: 25);

        var result = await check.PerformCheck();

        Assert.That(result.HasFailed, Is.True, "20% remaining is below the configured 25% threshold");
    }

    [Test]
    public async Task Fails_instead_of_dividing_by_zero_when_the_drive_reports_no_size()
    {
        var check = CreateCheck(availableFreeSpace: 0, threshold: 15, totalSize: 0);

        var result = await check.PerformCheck();

        Assert.That(result.HasFailed, Is.True);
        Assert.That(result.FailureReason, Does.Contain(StoragePath));
    }

    [Test]
    public async Task Fails_when_the_drive_cannot_be_read()
    {
        var check = new FileSystemBodyStorageCustomCheck(
            CreateSettings(StoragePath, 15),
            new ThrowingDriveSpaceProvider(new DriveNotFoundException("Could not find the drive 'Z:\\'.")),
            NullLogger<FileSystemBodyStorageCustomCheck>.Instance);

        var result = await check.PerformCheck();

        Assert.That(result.HasFailed, Is.True);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.FailureReason, Does.Contain(StoragePath));
            Assert.That(result.FailureReason, Does.Contain("Could not find the drive"));
        }
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("bodies")]
    [TestCase("bodies/nested")]
    public async Task Fails_when_the_storage_path_has_no_drive_root(string storagePath)
    {
        var provider = new FakeDriveSpaceProvider(1000, TotalSize);
        var check = new FileSystemBodyStorageCustomCheck(
            CreateSettings(storagePath, 15),
            provider,
            NullLogger<FileSystemBodyStorageCustomCheck>.Instance);

        var result = await check.PerformCheck();

        Assert.That(result.HasFailed, Is.True);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.FailureReason, Does.Contain("An absolute path is required."));
            Assert.That(provider.WasCalled, Is.False);
        }
    }

    [TestCase(0, false)]
    [TestCase(100, true)]
    public async Task Reads_the_real_drive_hosting_the_storage_path(int threshold, bool expectedToFail)
    {
        var check = new FileSystemBodyStorageCustomCheck(
            CreateSettings(Path.GetTempPath(), threshold),
            new DriveInfoSpaceProvider(),
            NullLogger<FileSystemBodyStorageCustomCheck>.Instance);

        var result = await check.PerformCheck();

        Assert.That(result.HasFailed, Is.EqualTo(expectedToFail), result.FailureReason);
    }

    [Test]
    public void Is_registered_and_resolvable_for_file_system_body_storage()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        RegisterDataStores(services, new TestPersisterSettings
        {
            ConnectionString = "Server=nowhere",
            BodyStorage = CreateSettings(StoragePath, 15)
        });

        var check = services.BuildServiceProvider().GetServices<ICustomCheck>().OfType<FileSystemBodyStorageCustomCheck>().SingleOrDefault();

        Assert.That(check, Is.Not.Null);
    }

    [Test]
    public void Is_not_registered_for_other_body_storage_types()
    {
        var services = new ServiceCollection();
        RegisterDataStores(services, new TestPersisterSettings
        {
            ConnectionString = "Server=nowhere",
            BodyStorage = new S3BodyStorageSettings { BucketName = "bodies" }
        });

        Assert.That(services.Any(descriptor => descriptor.ImplementationType == typeof(FileSystemBodyStorageCustomCheck)), Is.False);
    }

    [Test]
    public void Reports_itself_under_a_stable_identity()
    {
        var check = CreateCheck(availableFreeSpace: 200, threshold: 15);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(check.Id, Is.EqualTo("ServiceControl body storage"));
            Assert.That(check.Category, Is.EqualTo("Storage space"));
            Assert.That(check.Interval, Is.EqualTo(TimeSpan.FromMinutes(15)));
        }
    }

    static FileSystemBodyStorageCustomCheck CreateCheck(long availableFreeSpace, int threshold, long totalSize = TotalSize) =>
        new(CreateSettings(StoragePath, threshold),
            new FakeDriveSpaceProvider(availableFreeSpace, totalSize),
            NullLogger<FileSystemBodyStorageCustomCheck>.Instance);

    static FileSystemBodyStorageSettings CreateSettings(string storagePath, int threshold) =>
        new() { StoragePath = storagePath, DataSpaceRemainingThreshold = threshold };

    static string StoragePath { get; } = Path.Combine(Path.GetTempPath(), "bodies");

    class FakeDriveSpaceProvider(long availableFreeSpace, long totalSize) : IDriveSpaceProvider
    {
        public bool WasCalled { get; private set; }

        public DriveSpace GetDriveSpace(string pathRoot)
        {
            WasCalled = true;

            return new DriveSpace(pathRoot, availableFreeSpace, totalSize);
        }
    }

    class ThrowingDriveSpaceProvider(Exception exception) : IDriveSpaceProvider
    {
        public DriveSpace GetDriveSpace(string pathRoot) => throw exception;
    }

    sealed class TestPersisterSettings : EFPersisterSettings;
}
