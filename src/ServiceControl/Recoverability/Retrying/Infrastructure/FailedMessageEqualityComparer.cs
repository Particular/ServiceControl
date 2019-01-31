using System.Collections.Generic;

namespace ServiceControl.Recoverability
{
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
    }
}