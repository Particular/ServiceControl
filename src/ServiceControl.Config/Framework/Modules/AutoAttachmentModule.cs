namespace ServiceControl.Config.Framework.Modules
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Autofac;
    using Autofac.Core;
    using Caliburn.Micro;

    public class AutoAttachmentModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterAssemblyTypes(AssemblySource.Instance.ToArray())
                .Where(type => type.Name.EndsWith("Attachment"))
                .AsClosedTypesOf(typeof(Attachment<>))
                .InstancePerDependency();
        }

        protected override void AttachToComponentRegistration(IComponentRegistry componentRegistry, IComponentRegistration registration)
        {
            registration.Activated += OnComponentActivated;
        }

        void OnComponentActivated(object sender, ActivatedEventArgs<object> e)
        {
            var vmType = e.Instance.GetType();

            if (!vmType.FullName.EndsWith("ViewModel"))
            {
                return;
            }

            var attachmentBaseType = typeof(Attachment<>).MakeGenericType(vmType);
            var attachmentCollectionType = typeof(IEnumerable<>).MakeGenericType(attachmentBaseType);
            var attachments = (IEnumerable)e.Context.Resolve(attachmentCollectionType);

            foreach (IAttachment attachment in attachments)
            {
                attachment.AttachTo(e.Instance);
            }
        }
    }
}