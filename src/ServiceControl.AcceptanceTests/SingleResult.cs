namespace ServiceBus.Management.AcceptanceTests
{
    using System.Threading.Tasks;

    public class SingleResult<T>
    {
        public static SingleResult<T> Empty = new SingleResult<T>
        {
            HasResult = false, Item = default(T)
        };

        public static SingleResult<T> New(T item)
        {
            return new SingleResult<T> { HasResult = true, Item = item };
        }

        public bool HasResult { get; private set; }
        public T Item { get; private set; }

        public static implicit operator bool(SingleResult<T> result)
        {
            return result.HasResult;
        }

        public static implicit operator Task<bool>(SingleResult<T> result)
        {
            return Task.FromResult(result.HasResult);
        }

        public static implicit operator T(SingleResult<T> result)
        {
            return result.Item;
        }
    }
}