namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using NUnit.Framework;

    [AttributeUsage(AttributeTargets.Method)]
    public class IgnoreTransportsAttribute : PropertyAttribute
    {
        public IgnoreTransportsAttribute(params Type[] transportTypesToIgnore) : base(transportTypesToIgnore) { }
    }
}