namespace ServiceControl.Recoverability;

using System;

class ErrorQueueNameCache
{
    public string ResolvedErrorAddress
    {
        get
        {
            if (string.IsNullOrEmpty(field))
            {
                throw new InvalidOperationException($"{nameof(ResolvedErrorAddress)} is not set. Please set it before accessing.");
            }

            return field;
        }
        set;
    }
}
