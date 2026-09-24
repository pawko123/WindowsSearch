using System.Net;
using WindowsSearch.Common.Serialization;

namespace BaseProvider.Transports;

internal static class TransportMessageCodec
{
    private static IMessageSerializer _serializer = new JsonMessageSerializer();

    public static void Initialize(IMessageSerializer serializer)
    {
        _serializer = serializer;
    }

    public static string ContentType => _serializer.ContentType;

    public static byte[] Serialize<T>(T value) => _serializer.Serialize(value);

    public static T Deserialize<T>(byte[] bytes) => _serializer.Deserialize<T>(bytes) ?? throw new InvalidOperationException("Unable to deserialize provider message.");

    public static async Task WriteFrameAsync(Stream stream, byte[] payload, CancellationToken cancellationToken)
    {
        var lengthPrefix = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(payload.Length));
        await stream.WriteAsync(lengthPrefix, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static async Task<byte[]> ReadFrameAsync(Stream stream, CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[4];
        await ReadExactlyAsync(stream, lengthBuffer, cancellationToken);
        var length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lengthBuffer, 0));
        var payload = new byte[length];
        await ReadExactlyAsync(stream, payload, cancellationToken);
        return payload;
    }

    private static async Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            offset += read;
        }
    }
}
