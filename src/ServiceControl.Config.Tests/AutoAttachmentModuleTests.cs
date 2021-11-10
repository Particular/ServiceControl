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