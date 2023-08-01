namespace NServiceBus.Transport.Msmq
{
    using System.Collections.Generic;
    using System.Linq;
    using DeliveryConstraints;

    static class DeliveryConstraintExtensions
    {
        public static bool TryGet<T>(this List<DeliveryConstraint> list, out T constraint) where T : DeliveryConstraint =>
            (constraint = list.OfType<T>().FirstOrDefault()) != null;
    }
}