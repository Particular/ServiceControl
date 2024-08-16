namespace ServiceControl.Config.Tests
{
    using Autofac;
    using Caliburn.Micro;
    using Framework;
    using Framework.Modules;
    using NUnit.Framework;

    [TestFixture]
    public class AutoAttachmentModuleTests
    {
        [OneTimeSetUp]
        public static void SetUp() => AssemblySource.Instance.Add(typeof(AutoAttachmentModuleTests).Assembly);

        [OneTimeTearDown]
        public static void TearDown() => AssemblySource.Instance.Remove(typeof(AutoAttachmentModuleTests).Assembly);

        [Test]
        public void AttachmentsAreAttached()
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule(new AutoAttachmentModule());
            builder.RegisterType<TargetViewModel>();

            var container = builder.Build();

            var viewModel = container.Resolve<TargetViewModel>();
            Assert.Multiple(() =>
            {
                Assert.That(viewModel.FirstAttachmentActivated, Is.True, "First Attachment should have been attached");
                Assert.That(viewModel.SecondAttachmentActivated, Is.True, "Second Attachment should have been attached");
            });
        }

        [Test]
        public void IgnoresAttachmentsForNonViewModelSuffix()
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule(new AutoAttachmentModule());
            builder.RegisterType<ViewModelWithoutSuffix>();

            var container = builder.Build();

            var viewModel = container.Resolve<ViewModelWithoutSuffix>();
            Assert.That(viewModel.AttachmentActivated, Is.False, "Attachment should not be activated when the target type name does not have a ViewModel suffix");
        }

        [Test]
        public void IgnoresAttachmentsWithoutAttachmentSuffix()
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule(new AutoAttachmentModule());
            builder.RegisterType<HasBadAttachmentViewModel>();

            var container = builder.Build();

            var viewModel = container.Resolve<HasBadAttachmentViewModel>();
            Assert.That(viewModel.AttachmentActivated, Is.False, "Attachment should not be activated when it is missing the Attachment suffix");
        }
    }

    public class HasBadAttachmentViewModel
    {
        public bool AttachmentActivated { get; set; }
    }

    public class AttachmentThatIsMissingSuffix : Attachment<HasBadAttachmentViewModel>
    {
        protected override void OnAttach() => viewModel.AttachmentActivated = true;
    }

    public class ViewModelWithoutSuffix
    {
        public bool AttachmentActivated { get; set; }
    }

    public class NonActivatedAttachment : Attachment<ViewModelWithoutSuffix>
    {
        protected override void OnAttach() => viewModel.AttachmentActivated = true;
    }

    public class TargetViewModel
    {
        public bool FirstAttachmentActivated { get; set; }
        public bool SecondAttachmentActivated { get; set; }
    }

    public class FirstAttachment : Attachment<TargetViewModel>
    {
        protected override void OnAttach() => viewModel.FirstAttachmentActivated = true;
    }

    public class SecondAttachment : Attachment<TargetViewModel>
    {
        protected override void OnAttach() => viewModel.SecondAttachmentActivated = true;
    }
}