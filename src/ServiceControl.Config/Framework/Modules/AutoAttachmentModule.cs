namespace ServiceControl.Config.Framework.Modules
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Autofac;
    using Autofac.Core;
    using Autofac.Core.Registration;
    using Autofac.Core.Resolving.Pipeline;
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

        protected override void AttachToComponentRegistration(IComponentRegistryBuilder componentRegistryBuilder, IComponentRegistration registration)
        {
            registration.PipelineBuilding += (sender, builder) =>
            {
                builder.Use(PipelinePhase.Activation, (context, callback) =>
                {
                    callback(context);
                    OnComponentActivated(context);
                });
            };
        }

        void OnComponentActivated(ResolveRequestContext e)
        {
            if (e.Instance == null)
            {
                return;
            }
            var vmType = e.Instance.GetType();

            if (!vmType.FullName.EndsWith("ViewModel"))
            {
                return;
            }

            var attachmentBaseType = typeof(Attachment<>).MakeGenericType(vmType);
            var attachmentCollectionType = typeof(IEnumerable<>).MakeGenericType(attachmentBaseType);
            var attachments = (IEnumerable)e.Resolve(attachmentCollectionType);

            foreach (IAttachment attachment in attachments)
            {
                attachment.AttachTo(e.Instance);
            }
        }
    }
}