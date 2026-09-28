using BaseProvider.Transports;

namespace BaseProvider.Tests.Transports;

public class TransportMessageCodecTests
{
    [Fact]
    public async Task WriteAndReadFrameAsync_EndToEnd_ReadsExactPayload()
    {
        var payload = "Test payload"u8.ToArray();
        using var stream = new MemoryStream();

        // Write
        await TransportMessageCodec.WriteFrameAsync(stream, payload, CancellationToken.None);
        
        // Reset stream position
        stream.Position = 0;

        // Read
        var readPayload = await TransportMessageCodec.ReadFrameAsync(stream, CancellationToken.None);

        Assert.Equal(payload, readPayload);
    }

    [Fact]
    public async Task ReadFrameAsync_IncompleteStream_ThrowsEndOfStreamException()
    {
        using var stream = new MemoryStream();
        // Write only 2 bytes (incomplete length prefix)
        stream.Write(new byte[] { 0x00, 0x00 });
        stream.Position = 0;

        await Assert.ThrowsAsync<EndOfStreamException>(
            () => TransportMessageCodec.ReadFrameAsync(stream, CancellationToken.None));
    }
}
