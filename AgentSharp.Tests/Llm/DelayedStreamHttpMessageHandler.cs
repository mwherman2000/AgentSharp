using System.IO.Pipelines;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace AgentSharp.Tests.Llm;

/// <summary>
/// Returns a streaming response whose body is written out as a sequence of chunks,
/// each preceded by an artificial delay -- lets tests simulate a slow-but-alive SSE
/// connection (small gaps between chunks) versus a genuinely stalled one (one big gap),
/// without touching the network. Uses a Pipe so the reader sees each chunk as soon as
/// it's written rather than only after the whole body exists: HttpContent's default
/// ReadAsStreamAsync() drives CreateContentReadStreamAsync() through
/// SerializeToStreamAsync to completion *before* returning a stream, which would
/// defeat the whole point here (as opposed to StubHttpMessageHandler, which only
/// returns a response after its full body already exists -- fine for non-streaming
/// tests, but useless for testing idle-timeout behavior).
/// </summary>
internal class DelayedStreamHttpMessageHandler : HttpMessageHandler
{
    private readonly IReadOnlyList<(string Chunk, TimeSpan DelayBefore)> _chunks;

    public DelayedStreamHttpMessageHandler(IReadOnlyList<(string Chunk, TimeSpan DelayBefore)> chunks)
    {
        _chunks = chunks;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new DelayedChunksContent(_chunks)
        };
        return Task.FromResult(response);
    }

    private class DelayedChunksContent : HttpContent
    {
        private readonly IReadOnlyList<(string Chunk, TimeSpan DelayBefore)> _chunks;

        public DelayedChunksContent(IReadOnlyList<(string Chunk, TimeSpan DelayBefore)> chunks)
        {
            _chunks = chunks;
            Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
        }

        protected override Task<Stream> CreateContentReadStreamAsync()
        {
            var pipe = new Pipe();
            _ = PumpAsync(pipe.Writer);
            return Task.FromResult(pipe.Reader.AsStream());
        }

        private async Task PumpAsync(PipeWriter writer)
        {
            try
            {
                foreach (var (chunk, delayBefore) in _chunks)
                {
                    if (delayBefore > TimeSpan.Zero)
                        await Task.Delay(delayBefore);

                    var bytes = Encoding.UTF8.GetBytes(chunk);
                    await writer.WriteAsync(bytes);
                }
                await writer.CompleteAsync();
            }
            catch (Exception ex)
            {
                await writer.CompleteAsync(ex);
            }
        }

        // Only the CreateContentReadStreamAsync path above is exercised by
        // AnthropicClient/OpenAiCompatibleClient (they call response.Content.
        // ReadAsStreamAsync(ct), never CopyToAsync), so this is intentionally unused.
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => throw new NotSupportedException("This test fake only supports the ReadAsStreamAsync path.");

        // Unknown length matches real SSE responses (no Content-Length up front) and
        // is required for the incremental-read behavior above to mean anything.
        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
