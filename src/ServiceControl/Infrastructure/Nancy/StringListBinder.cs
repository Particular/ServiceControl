namespace ServiceControl.Infrastructure.Nancy
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using global::Nancy;
    using global::Nancy.Extensions;
    using global::Nancy.ModelBinding;

    public class StringListBinder : IModelBinder
    {
        private static readonly MethodInfo toListMethodInfo = typeof (Enumerable).GetMethod("ToList",
            BindingFlags.Public | BindingFlags.Static);

        private readonly IEnumerable<IBodyDeserializer> bodyDeserializers;
        private readonly BindingDefaults defaults;
        private readonly IFieldNameConverter fieldNameConverter;
        private readonly IEnumerable<ITypeConverter> typeConverters;

        public StringListBinder(IEnumerable<IBodyDeserializer> bodyDeserializers, IFieldNameConverter fieldNameConverter,
            BindingDefaults defaults, IEnumerable<ITypeConverter> typeConverters)
        {
            this.bodyDeserializers = bodyDeserializers;
            this.fieldNameConverter = fieldNameConverter;
            this.defaults = defaults;
            this.typeConverters = typeConverters;
        }

        /// <summary>
        ///     Whether the binder can bind to the given model type
        /// </summary>
        /// <param name="modelType">Required model type</param>
        /// <returns>True if binding is possible, false otherwise</returns>
        public bool CanBind(Type modelType)
        {
            return typeof (IEnumerable<string>).IsAssignableFrom(modelType);
        }

        /// <summary>
        ///     Bind to the given model type
        /// </summary>
        /// <param name="context">Current context</param>
        /// <param name="modelType">Model type to bind to</param>
        /// <param name="instance">Optional existing instance</param>
        /// <param name="configuration">The <see cref="BindingConfig" /> that should be applied during binding.</param>
        /// <param name="blackList">Blacklisted property names</param>
        /// <returns>Bound model</returns>
        public object Bind(NancyContext context, Type modelType, object instance, BindingConfig configuration,
            params string[] blackList)
        {
            Type genericType = null;
            if (modelType.IsArray() || modelType.IsCollection() || modelType.IsEnumerable())
            {
                //make sure it has a generic type
                if (modelType.IsGenericType())
                {
                    genericType = modelType.GetGenericArguments().FirstOrDefault();
                }
                else
                {
                    var ienumerable =
                        modelType.GetInterfaces().Where(i => i.IsGenericType()).FirstOrDefault(
                            i => i.GetGenericTypeDefinition() == typeof (IEnumerable<>));
                    genericType = ienumerable == null ? null : ienumerable.GetGenericArguments().FirstOrDefault();
                }

                if (genericType == null)
                {
                    throw new ArgumentException("When modelType is an enumerable it must specify the type", "modelType");
                }
            }

            var bindingContext =
                CreateBindingContext(context, modelType, instance, configuration, blackList, genericType);

            var bodyDeserializedModel = DeserializeRequestBody(bindingContext);

            var list = (instance as IEnumerable<string>) ?? bodyDeserializedModel;

            return list;
        }

        private BindingContext CreateBindingContext(NancyContext context, Type modelType, object instance,
            BindingConfig configuration, IEnumerable<string> blackList, Type genericType)
        {
            return new BindingContext
            {
                Configuration = configuration,
                Context = context,
                DestinationType = modelType,
                Model = CreateModel(modelType, genericType, instance),
                ValidModelProperties = GetProperties(modelType, genericType, blackList),
                RequestData = GetDataFields(context),
                GenericType = genericType,
                TypeConverters = typeConverters.Concat(defaults.DefaultTypeConverters),
            };
        }

        private static IEnumerable<PropertyInfo> GetProperties(Type modelType, Type genericType,
            IEnumerable<string> blackList)
        {
            if (genericType != null)
            {
                return genericType
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanWrite && !blackList.Contains(p.Name, StringComparer.InvariantCulture))
                    .Where(property => !property.GetIndexParameters().Any());
            }
            return modelType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && !blackList.Contains(p.Name, StringComparer.InvariantCulture))
                .Where(property => !property.GetIndexParameters().Any());
        }

        private IDictionary<string, string> GetDataFields(NancyContext context)
        {
            var dictionaries = new IDictionary<string, string>[]
            {
                ConvertDynamicDictionary(context.Request.Form),
                ConvertDynamicDictionary(context.Request.Query),
                ConvertDynamicDictionary(context.Parameters)
            };

            return dictionaries.Merge();
        }

        private IDictionary<string, string> ConvertDynamicDictionary(DynamicDictionary dictionary)
        {
            if (dictionary == null)
            {
                return null;
            }

            return dictionary.GetDynamicMemberNames().ToDictionary(
                memberName => fieldNameConverter.Convert(memberName),
                memberName => (string) dictionary[memberName]);
        }

        private static object CreateModel(Type modelType, Type genericType, object instance)
        {
            if (modelType.IsArray() || modelType.IsCollection() || modelType.IsEnumerable())
            {
                //make sure instance has a Add method. Otherwise call `.ToList`
                if (instance != null && modelType.IsInstanceOfType(instance))
                {
                    var addMethod = modelType.GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
                    if (addMethod != null)
                    {
                        return instance;
                    }
                    var genericMethod = toListMethodInfo.MakeGenericMethod(genericType);
                    return genericMethod.Invoke(null, new[] {instance});
                }

                //else just make a list
                var listType = typeof (List<>).MakeGenericType(genericType);
                return Activator.CreateInstance(listType);
            }

            if (instance == null)
            {
                return Activator.CreateInstance(modelType);
            }

            return !modelType.IsInstanceOfType(instance)
                ? Activator.CreateInstance(modelType)
                : instance;
        }

        private object DeserializeRequestBody(BindingContext context)
        {
            if (context.Context == null || context.Context.Request == null)
            {
                return null;
            }

            var contentType = GetRequestContentType(context.Context);
            var bodyDeserializer = bodyDeserializers.FirstOrDefault(b => b.CanDeserialize(contentType, context));

            if (bodyDeserializer != null)
            {
                return bodyDeserializer.Deserialize(contentType, context.Context.Request.Body, context);
            }

            bodyDeserializer =
                defaults.DefaultBodyDeserializers.FirstOrDefault(b => b.CanDeserialize(contentType, context));

            return bodyDeserializer != null
                ? bodyDeserializer.Deserialize(contentType, context.Context.Request.Body, context)
                : null;
        }

        private static string GetRequestContentType(NancyContext context)
        {
            if (context == null || context.Request == null)
            {
                return String.Empty;
            }

            var contentType =
                context.Request.Headers.ContentType;

            return (string.IsNullOrEmpty(contentType))
                ? string.Empty
                : contentType;
        }
    }
}