namespace ServiceControl.AcceptanceTesting
{
    using System;

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class RunOnAllDataStoresAttribute : Attribute
    {
    }
}