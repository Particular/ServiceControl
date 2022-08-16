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
        [Test]
        public void AttachmentsAreAttached()
        {
            AssemblySource.Instance.Add(typeof(AutoAttachmentModuleTests).Assembly);
            var builder = new ContainerBuilder();
            builder.RegisterModule(new AutoAttachmentModule());
            builder.RegisterType<TargetViewModel>();

            var container = builder.Build();

            var viewModel = container.Resolve<TargetViewModel>();
            Assert.IsTrue(viewModel.FirstAttachmentActivated, "First Attachment should have been attached");
            Assert.IsTrue(viewModel.SecondAttachmentActivated, "Second Attachment should have been attached");
        }

        [Test]
        public void IgnoresAttachmentsForNonViewModelSuffix()
        {
            AssemblySource.Instance.Add(typeof(AutoAttachmentModuleTests).Assembly);
            var builder = new ContainerBuilder();
            builder.RegisterModule(new AutoAttachmentModule());
            builder.RegisterType<ViewModelWithoutSuffix>();

            var container = builder.Build();

            var viewModel = container.Resolve<ViewModelWithoutSuffix>();
            Assert.IsFalse(viewModel.AttachmentActivated, "Attachment should not be activated when the target type name does not have a ViewModel suffix");
        }

        [Test]
        public void IgnoresAttachmentsWithoutAttachmentSuffix()
        {
            AssemblySource.Instance.Add(typeof(AutoAttachmentModuleTests).Assembly);
            var builder = new ContainerBuilder();
            builder.RegisterModule(new AutoAttachmentModule());
            builder.RegisterType<HasBadAttachmentViewModel>();

            var container = builder.Build();

            var viewModel = container.Resolve<HasBadAttachmentViewModel>();
            Assert.IsFalse(viewModel.AttachmentActivated, "Attachment should not be activated when it is missing the Attachment suffix");
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