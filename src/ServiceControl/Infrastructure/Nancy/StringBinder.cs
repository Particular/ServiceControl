namespace ServiceBus.Management.Infrastructure.Nancy
{
    using System;
    using System.IO;
    using global::Nancy;
    using global::Nancy.ModelBinding;

    public class StringBinder : IModelBinder
    {
        public object Bind(NancyContext context, Type modelType, object instance, BindingConfig configuration, params string[] blackList)
        {
            using (var reader = new StreamReader(context.Request.Body))
            {
                return reader.ReadToEnd();
            }
        }

        public bool CanBind(Type modelType)
        {
            return modelType == typeof(String);
        }
    }
}