namespace ServiceControl.Migrations
{
    using System;

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class MigrationAttribute : Attribute
    {
        public MigrationAttribute(long executionOrder)
        {
            ExecutionOrder = executionOrder;
        }

        public long ExecutionOrder { get; set; }
    }
}