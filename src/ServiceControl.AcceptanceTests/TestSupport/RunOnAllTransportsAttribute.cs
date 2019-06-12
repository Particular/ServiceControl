namespace ServiceControl.AcceptanceTests
{
    using System;

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class RunOnAllTransportsAttribute : Attribute
    {
    }
}