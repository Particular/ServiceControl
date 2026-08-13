namespace ServiceControl.Operations.BodyStorage
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IBodyStorage
    {
        Task<MessageBodyResult> TryFetch(string bodyId, CancellationToken cancellationToken = default);
    }

    public enum MessageBodyState
    {
        NotFound,
        Empty,
        Unavailable,
        Available
    }

    public sealed class MessageBodyResult
    {
        MessageBodyResult(MessageBodyState state, MessageBodyStreamContent content = null)
        {
            State = state;
            ContentValue = content;
        }

        public MessageBodyState State { get; }

        public MessageBodyStreamContent Content => State == MessageBodyState.Available
            ? ContentValue
            : throw new InvalidOperationException($"Body content is not available when the state is {State}.");

        public static MessageBodyResult NotFound() => new(MessageBodyState.NotFound);

        public static MessageBodyResult Empty() => new(MessageBodyState.Empty);

        public static MessageBodyResult Unavailable() => new(MessageBodyState.Unavailable);

        public static MessageBodyResult Available(MessageBodyStreamContent content)
        {
            ArgumentNullException.ThrowIfNull(content);
            return new MessageBodyResult(MessageBodyState.Available, content);
        }

        MessageBodyStreamContent ContentValue { get; }
    }

    public sealed record MessageBodyStreamContent(Stream Stream, string ContentType, int BodySize, string Etag);
}