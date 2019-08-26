namespace NServiceBus.AcceptanceTests.EndpointTemplates
{
    using System;

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class RunOnAllTransportsAttribute : Attribute
    {
    }
}