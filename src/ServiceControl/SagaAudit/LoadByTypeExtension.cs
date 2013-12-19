namespace ServiceControl.SagaAudit
{
    using System;
    using Raven.Client;
    using Raven.Client.Util;

    public static class LoadByTypeExtension
    {
        public static T LoadEx<T>(this IDocumentSession session, Guid id)
        {
            var typeName = Inflector.Pluralize(typeof(T).Name);
            var lookupId = string.Format("{0}/{1}", typeName, id);
            return session.Load<T>(lookupId);
        }
    }
}