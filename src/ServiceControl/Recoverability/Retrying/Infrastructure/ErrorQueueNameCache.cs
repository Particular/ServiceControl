namespace ServiceControl.Recoverability
{
    using System;

    class ErrorQueueNameCache
    {
        string resolvedErrorAddress;

        public string ResolvedErrorAddress
        {
            get
            {
                if (string.IsNullOrEmpty(resolvedErrorAddress))
                {
                    throw new InvalidOperationException($"{nameof(ResolvedErrorAddress)} is not set. Please set it before accessing.");
                }

                return resolvedErrorAddress;
            }
            set => resolvedErrorAddress = value;
        }
    }
}
