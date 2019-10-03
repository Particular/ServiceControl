namespace ServiceControl.Audit.Infrastructure.WebApi
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    class CompressionEncodingHttpHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            response.Headers.TransferEncodingChunked = true;

            if (response.StatusCode != HttpStatusCode.OK)
            {
                return response;
            }

            var acceptedEncodingTypes = GetAcceptedEncodingTypes(request);

            if (acceptedEncodingTypes.Count > 0)
            {
                response.Content = new CompressedContent(response.Content, acceptedEncodingTypes);
            }

            return response;
        }

        static List<EncodingType> GetAcceptedEncodingTypes(HttpRequestMessage request)
        {
            var encodings = new List<EncodingType>();

            if (request.Headers.AcceptEncoding == null)
            {
                return encodings;
            }

            foreach (var encoding in request.Headers.AcceptEncoding)
            {
                if (encoding.Value != null && encoding.Value.ToLowerInvariant() == "gzip")
                {
                    encodings.Add(EncodingType.GZip);
                }

                if (encoding.Value != null && encoding.Value.ToLowerInvariant() == "deflate")
                {
                    encodings.Add(EncodingType.Deflate);
                }
            }

            return encodings;
        }
    }

    enum EncodingType
    {
        GZip,
        Deflate
    }

    class CompressedContent : HttpContent
    {
        public CompressedContent(HttpContent content, List<EncodingType> acceptedEncodingTypes)
        {
            if (acceptedEncodingTypes.Contains(EncodingType.GZip))
            {
                selectedEncodingType = EncodingType.GZip;
            }
            else if (acceptedEncodingTypes.Contains(EncodingType.Deflate))
            {
                selectedEncodingType = EncodingType.Deflate;
            }
            else
            {
                throw new NotSupportedException("Only GZip and Deflate compressions supported");
            }

            originalContent = content;

            // copy the headers from the original content
            foreach (var header in content.Headers)
            {
                Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            Headers.ContentEncoding.Add(selectedEncodingType.ToString().ToLowerInvariant());
        }

        protected override bool TryComputeLength(out long length)
        {
            length = -1;

            return false;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            Stream compressedStream = null;

            if (selectedEncodingType == EncodingType.GZip)
            {
                compressedStream = new GZipStream(stream, CompressionMode.Compress, true);
            }
            else if (selectedEncodingType == EncodingType.Deflate)
            {
                compressedStream = new DeflateStream(stream, CompressionMode.Compress, true);
            }

            try
            {
                await originalContent.CopyToAsync(compressedStream).ConfigureAwait(false);
            }
            finally
            {
                compressedStream?.Dispose();
            }
        }

        readonly HttpContent originalContent;
        readonly EncodingType selectedEncodingType;
    }
}