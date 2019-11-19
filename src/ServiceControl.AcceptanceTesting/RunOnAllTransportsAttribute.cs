namespace ServiceControl.AcceptanceTesting
{
    using System;

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class RunOnAllTransportsAttribute : Attribute
    {
    }
}