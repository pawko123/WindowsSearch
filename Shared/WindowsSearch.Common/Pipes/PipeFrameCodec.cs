using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipes;

namespace WindowsSearch.Common.Pipes;

/// <summary>
/// Provides zero-allocation framing and protocol helpers for NamedPipe communication.
/// </summary>
public static class PipeFrameCodec
{
    public const int DefaultMaxMessageSize = 16 * 1024 * 1024; // 16 MB
    public const int DefaultPipeBufferSize = 65536; // 64 KB
    public const PipeOptions DefaultServerPipeOptions = PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly;
    public const PipeOptions DefaultClientPipeOptions = PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly;

    public static async Task WriteFrameAsync(Stream stream, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // For payloads <= 1MB, combine header and payload in rented buffer for single syscall
        if (payload.Length <= 1024 * 1024)
        {
            int totalLength = 4 + payload.Length;
            byte[] rented = ArrayPool<byte>.Shared.Rent(totalLength);
            try
            {
                BinaryPrimitives.WriteInt32BigEndian(rented.AsSpan(0, 4), payload.Length);
                payload.Span.CopyTo(rented.AsSpan(4, payload.Length));
                await stream.WriteAsync(rented.AsMemory(0, totalLength), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
        else
        {
            byte[] header = ArrayPool<byte>.Shared.Rent(4);
            try
            {
                BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), payload.Length);
                await stream.WriteAsync(header.AsMemory(0, 4), cancellationToken).ConfigureAwait(false);
                await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(header);
            }
        }

        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<byte[]> ReadFrameAsync(Stream stream, int maxMessageSize = DefaultMaxMessageSize, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        byte[] headerBuffer = ArrayPool<byte>.Shared.Rent(4);
        int length;
        try
        {
            await stream.ReadExactlyAsync(headerBuffer.AsMemory(0, 4), cancellationToken).ConfigureAwait(false);
            length = BinaryPrimitives.ReadInt32BigEndian(headerBuffer.AsSpan(0, 4));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(headerBuffer);
        }

        if (length <= 0 || length > maxMessageSize)
        {
            throw new InvalidDataException($"Invalid frame length: {length} bytes (allowed range: 1 to {maxMessageSize} bytes).");
        }

        byte[] payload = GC.AllocateUninitializedArray<byte>(length);
        await stream.ReadExactlyAsync(payload.AsMemory(0, length), cancellationToken).ConfigureAwait(false);
        return payload;
    }

    public static string NormalizePipeName(string endpoint, string fallback = "windows_search_pipe")
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return fallback;
        }

        const string prefix = @"\\.\pipe\";
        return endpoint.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? endpoint[prefix.Length..]
            : endpoint;
    }
}
