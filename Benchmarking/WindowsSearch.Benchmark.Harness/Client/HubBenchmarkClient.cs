using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using WindowsSearch.Common.Models.Benchmark;

namespace WindowsSearch.Benchmark.Harness.Client;

public sealed class HubBenchmarkClient : IAsyncDisposable
{
    private readonly string pipeName;
    private NamedPipeClientStream? clientStream;
    private StreamReader? reader;
    private StreamWriter? writer;
    private readonly SemaphoreSlim communicationLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public HubBenchmarkClient(string pipeName = "hub_bench_control")
    {
        this.pipeName = NormalizePipeName(pipeName);
    }

    public async Task ConnectAsync(int timeoutMs = 10000, CancellationToken cancellationToken = default)
    {
        if (clientStream is { IsConnected: true })
        {
            return;
        }

        clientStream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await clientStream.ConnectAsync(timeoutMs, cancellationToken);
        reader = new StreamReader(clientStream, leaveOpen: true);
        writer = new StreamWriter(clientStream, leaveOpen: true) { AutoFlush = true };
    }

    public async Task<BenchmarkControlResponse> PingAsync(CancellationToken cancellationToken = default)
    {
        var command = new BenchmarkControlCommand { Action = "ping" };
        return await SendCommandAsync(command, cancellationToken);
    }

    public async Task<BenchmarkControlResponse> ApplySettingsAsync(BenchmarkControlCommand settingsCommand, CancellationToken cancellationToken = default)
    {
        settingsCommand.Action = "apply_settings";
        return await SendCommandAsync(settingsCommand, cancellationToken);
    }

    public async Task<BenchmarkControlResponse> SearchAsync(string query, int limit, bool bypassDebounce, string runId, CancellationToken cancellationToken = default)
    {
        var command = new BenchmarkControlCommand
        {
            Action = "search",
            Query = query,
            Limit = limit,
            BypassDebounce = bypassDebounce,
            RunId = runId
        };
        return await SendCommandAsync(command, cancellationToken);
    }

    public async Task<BenchmarkControlResponse> ShutdownAsync(CancellationToken cancellationToken = default)
    {
        var command = new BenchmarkControlCommand { Action = "shutdown" };
        return await SendCommandAsync(command, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (writer != null)
        {
            await writer.DisposeAsync();
        }
        reader?.Dispose();
        if (clientStream != null)
        {
            await clientStream.DisposeAsync();
        }
        communicationLock.Dispose();
    }

    private async Task<BenchmarkControlResponse> SendCommandAsync(BenchmarkControlCommand command, CancellationToken cancellationToken)
    {
        await communicationLock.WaitAsync(cancellationToken);
        try
        {
            if (clientStream is not { IsConnected: true })
            {
                await ConnectAsync(10000, cancellationToken);
            }

            var requestJson = JsonSerializer.Serialize(command);
            await writer!.WriteLineAsync(requestJson.AsMemory(), cancellationToken);

            var responseLine = await reader!.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(responseLine))
            {
                throw new InvalidOperationException("Hub closed pipe connection unexpectedly.");
            }

            return JsonSerializer.Deserialize<BenchmarkControlResponse>(responseLine, JsonOptions)
                   ?? throw new InvalidOperationException("Empty response received from Hub.");
        }
        finally
        {
            communicationLock.Release();
        }
    }

    private static string NormalizePipeName(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return "hub_bench_control";
        }

        const string prefix = @"\\.\pipe\";
        return endpoint.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? endpoint[prefix.Length..]
            : endpoint;
    }
}
