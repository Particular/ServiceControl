namespace ServiceBus.Management.Api.Nancy
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using global::Nancy;
    using global::Nancy.ModelBinding;

    /// <summary>
    /// Locates model binders for a particular model
    /// </summary>
    public class OverrideDefaultModelBinderLocatorBecauseWeHaveABugThatICantShakeIt : IModelBinderLocator
    {
        /// <summary>
        /// Available model binders
        /// </summary>
        private readonly IEnumerable<IModelBinder> binders;

        /// <summary>
        /// Default model binder to fall back on
        /// </summary>
        private readonly IBinder fallbackBinder;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultModelBinderLocator"/> class.
        /// </summary>
        /// <param name="binders">Available model binders</param>
        /// <param name="fallbackBinder">Fallback binder</param>
        public OverrideDefaultModelBinderLocatorBecauseWeHaveABugThatICantShakeIt(IEnumerable<IModelBinder> binders, DefaultBinder fallbackBinder)
        {
            // !!!HACK!!!
            // I had to change the ctor to be explicit about the fallback IBinder I want because otherwise the default 
            // impl (see https://github.com/NancyFx/Nancy/blob/master/src/Nancy/ModelBinding/DefaultModelBinderLocator.cs#L27) 
            // seems to not work with Autofac because we actually get StringListBinder resolving!
            this.fallbackBinder = fallbackBinder;
            this.binders = binders;
        }

        /// <summary>
        /// Gets a binder for the given type
        /// </summary>
        /// <param name="modelType">Destination type to bind to</param>
        /// <param name="context">The <see cref="NancyContext"/> instance of the current request.</param>
        /// <returns>IModelBinder instance or null if none found</returns>
        public IBinder GetBinderForType(Type modelType, NancyContext context)
        {
            return binders.FirstOrDefault(modelBinder => modelBinder.CanBind(modelType)) ?? fallbackBinder;
        }
    }
}