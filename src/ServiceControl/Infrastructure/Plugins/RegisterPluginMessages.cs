namespace ServiceControl.Infrastructure.Plugins
{
    using NServiceBus;
    using NServiceBus.Features;

    public class RegisterPluginMessagesFeature : Feature
    {
        public RegisterPluginMessagesFeature()
        {
            EnableByDefault();
        }

        /// <summary>
        /// Invoked if the feature is activated
        /// </summary>
        /// <param name="context">The feature context</param>
        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Settings.Get<Conventions>().AddSystemMessagesConventions(t => t.Namespace != null
                                                                                  && t.Namespace.StartsWith("ServiceControl.Plugin.")
                                                                                  && t.Namespace.EndsWith(".Messages"));
        }
    }
}