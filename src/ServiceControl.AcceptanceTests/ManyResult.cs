namespace ServiceBus.Management.AcceptanceTests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class ManyResult<T>
    {
        public static ManyResult<T> Empty = new ManyResult<T>
        {
            HasResult = false, Items = new List<T>()
        };

        public static ManyResult<T> New(bool hasResult, List<T> items)
        {
            return new ManyResult<T> { HasResult = hasResult, Items = items };
        }

        public bool HasResult { get; private set; }
        public List<T> Items { get; private set; }

        public static implicit operator bool(ManyResult<T> result)
        {
            return result.HasResult;
        }

        public static implicit operator Task<bool>(ManyResult<T> result)
        {
            return Task.FromResult(result.HasResult);
        }

        public static implicit operator List<T>(ManyResult<T> result)
        {
            return result.Items;
        }
    }
}