using WindowsSearch.Common.Pipes;
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

    public static Task WriteFrameAsync(Stream stream, byte[] payload, CancellationToken cancellationToken)
    {
        return PipeFrameCodec.WriteFrameAsync(stream, payload, cancellationToken);
    }

    public static Task<byte[]> ReadFrameAsync(Stream stream, CancellationToken cancellationToken)
    {
        return PipeFrameCodec.ReadFrameAsync(stream, PipeFrameCodec.DefaultMaxMessageSize, cancellationToken);
    }
}
