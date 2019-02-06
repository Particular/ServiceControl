namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;

    class FailedMessageEqualityComparer : IEqualityComparer<FailedMessageRetry>
    {
        public bool Equals(FailedMessageRetry x, FailedMessageRetry y)
        {
            return x.FailedMessageId == y.FailedMessageId;
        }

        public int GetHashCode(FailedMessageRetry obj)
        {
            return obj.FailedMessageId.GetHashCode();
        }

        public static readonly FailedMessageEqualityComparer Instance = new FailedMessageEqualityComparer();
    }
}