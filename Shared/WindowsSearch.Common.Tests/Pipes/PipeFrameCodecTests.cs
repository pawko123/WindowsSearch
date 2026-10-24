using System.Buffers.Binary;
using WindowsSearch.Common.Pipes;
using Xunit;

namespace WindowsSearch.Common.Tests.Pipes;

public class PipeFrameCodecTests
{
    [Fact]
    public async Task WriteAndReadFrameAsync_EndToEnd_ReadsExactPayload()
    {
        var payload = "Hello NamedPipe Frame Codec"u8.ToArray();
        using var stream = new MemoryStream();

        await PipeFrameCodec.WriteFrameAsync(stream, payload, CancellationToken.None);
        stream.Position = 0;

        var restored = await PipeFrameCodec.ReadFrameAsync(stream, PipeFrameCodec.DefaultMaxMessageSize, CancellationToken.None);

        Assert.Equal(payload, restored);
    }

    [Fact]
    public async Task WriteAndReadFrameAsync_LargePayloadOver1MB_ReadsExactPayload()
    {
        var payload = new byte[1024 * 1024 + 128]; // > 1 MB to test the non-rented fallback path
        new Random(42).NextBytes(payload);
        using var stream = new MemoryStream();

        await PipeFrameCodec.WriteFrameAsync(stream, payload, CancellationToken.None);
        stream.Position = 0;

        var restored = await PipeFrameCodec.ReadFrameAsync(stream, PipeFrameCodec.DefaultMaxMessageSize, CancellationToken.None);

        Assert.Equal(payload, restored);
    }

    [Fact]
    public async Task ReadFrameAsync_IncompleteStream_ThrowsEndOfStreamException()
    {
        using var stream = new MemoryStream();
        stream.Write([0x00, 0x00]); // Only 2 bytes of header
        stream.Position = 0;

        await Assert.ThrowsAsync<EndOfStreamException>(
            () => PipeFrameCodec.ReadFrameAsync(stream, PipeFrameCodec.DefaultMaxMessageSize, CancellationToken.None));
    }

    [Fact]
    public async Task ReadFrameAsync_ExceedsMaxMessageSize_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, 100); // Header indicates 100 bytes
        stream.Write(header);
        stream.Position = 0;

        // Set max limit to 50 bytes -> should throw InvalidDataException
        await Assert.ThrowsAsync<InvalidDataException>(
            () => PipeFrameCodec.ReadFrameAsync(stream, maxMessageSize: 50, CancellationToken.None));
    }

    [Fact]
    public async Task ReadFrameAsync_ZeroOrNegativeLength_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, -5);
        stream.Write(header);
        stream.Position = 0;

        await Assert.ThrowsAsync<InvalidDataException>(
            () => PipeFrameCodec.ReadFrameAsync(stream, PipeFrameCodec.DefaultMaxMessageSize, CancellationToken.None));
    }

    [Theory]
    [InlineData(@"\\.\pipe\firefox_search", "firefox_search")]
    [InlineData("chrome_search", "chrome_search")]
    [InlineData("", "windows_search_pipe")]
    [InlineData(null, "windows_search_pipe")]
    public void NormalizePipeName_NormalizesCorrectly(string? endpoint, string expected)
    {
        var result = PipeFrameCodec.NormalizePipeName(endpoint!, "windows_search_pipe");
        Assert.Equal(expected, result);
    }
}
