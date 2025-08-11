using System.IO.Compression;

namespace System.Net.Http;

public static class HttpResponseMessageExtensions
{
    public static async Task<string> ReadDecompressedContentAsync(this HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentEncoding.Contains("gzip"))
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var gzip = new GZipStream(stream, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            return await reader.ReadToEndAsync(cancellationToken);
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}