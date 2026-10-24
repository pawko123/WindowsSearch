using System.IO.Pipes;
using BaseProvider.Transports;
using WindowsSearch.Common.Models;
using WindowsSearch.Common.Pipes;
using WindowsSearch.Common.Serialization;

namespace BaseProvider.Tests.Transports;

public class NamedPipeProviderTransportTests
{
    [Fact]
    public async Task RunAsync_ClientConnectsAndSendsRequest_ReturnsValidResponse()
    {
        var pipeName = $"test_pipe_{Guid.NewGuid():N}";
        var transport = new NamedPipeProviderTransport(pipeName);
        var serializer = new JsonMessageSerializer();
        TransportMessageCodec.Initialize(serializer);

        using var cts = new CancellationTokenSource();

        var serverTask = Task.Run(async () =>
        {
            await transport.RunAsync((req, token) =>
            {
                var resp = new ProviderSearchResponse
                {
                    ProviderExecutionMs = 5.5,
                    Categories =
                    [
                        new ProviderResultCategory
                        {
                            Name = "TestCat",
                            Items = [new ProviderResultItem { Title = $"Result for {req.Query}" }]
                        }
                    ]
                };
                return Task.FromResult(resp);
            }, cts.Token);
        });

        // Give the server a moment to begin listening
        await Task.Delay(100);

        // Client side
        await using var clientStream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeFrameCodec.DefaultClientPipeOptions);
        await clientStream.ConnectAsync(3000, CancellationToken.None);

        var request = new ProviderSearchRequest { Query = "hello", Limit = 5 };
        var reqBytes = serializer.Serialize(request);
        await PipeFrameCodec.WriteFrameAsync(clientStream, reqBytes, CancellationToken.None);

        var respBytes = await PipeFrameCodec.ReadFrameAsync(clientStream, PipeFrameCodec.DefaultMaxMessageSize, CancellationToken.None);
        var response = serializer.Deserialize<ProviderSearchResponse>(respBytes);

        Assert.NotNull(response);
        Assert.Equal(5.5, response.ProviderExecutionMs);
        Assert.Single(response.Categories);
        Assert.Equal("Result for hello", response.Categories[0].Items[0].Title);

        // Clean shutdown
        cts.Cancel();
        await clientStream.DisposeAsync();
        await transport.DisposeAsync();

        try
        {
            await serverTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (OperationCanceledException) { }
    }
}
