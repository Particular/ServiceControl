namespace ServiceControl.Operations.BodyStorage
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IBodyStorage
    {
        /// <summary>
        /// Resolves a message body from wherever it was stored. A persister that holds audit data as
        /// well as failed messages resolves in one fixed order, because a message that both failed and
        /// was audited has two bodies and an edited message's two bodies differ:
        /// <list type="number">
        /// <item>failed message by UniqueMessageId,</item>
        /// <item>failed message by MessageId,</item>
        /// <item>audit message by UniqueMessageId, including a body held inline for full text search.</item>
        /// </list>
        /// </summary>
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
        MessageBodyResult(MessageBodyState state, MessageBodyStreamContent? content = null)
        {
            State = state;
            ContentValue = content;
        }

        public MessageBodyState State { get; }

        public MessageBodyStreamContent Content => State == MessageBodyState.Available && ContentValue is not null
            ? ContentValue
            : throw new InvalidOperationException($"Body content is not available when the state is {State} or content is null.");

        public static MessageBodyResult NotFound() => new(MessageBodyState.NotFound);

        public static MessageBodyResult Empty() => new(MessageBodyState.Empty);

        public static MessageBodyResult Unavailable() => new(MessageBodyState.Unavailable);

        public static MessageBodyResult Available(MessageBodyStreamContent content)
        {
            ArgumentNullException.ThrowIfNull(content);
            return new MessageBodyResult(MessageBodyState.Available, content);
        }

        MessageBodyStreamContent? ContentValue { get; }
    }

    public sealed record MessageBodyStreamContent(Stream Stream, string ContentType, int BodySize, string Etag);
}