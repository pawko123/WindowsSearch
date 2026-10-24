using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Security.Cryptography;
using WindowsSearch.Common.Pipes;
using WindowsSearch.Common.Security;
using System.Windows;
using System.Windows.Threading;
using Hub.Models.Settings;
using Hub.ViewModels;
using WindowsSearch.Common.Logging;
using WindowsSearch.Common.Models;
using WindowsSearch.Common.Models.Benchmark;
using WindowsSearch.Common.Serialization;

namespace Hub.Services.Benchmark;

public sealed class BenchmarkControlService : IAsyncDisposable
{
    private readonly string pipeName;
    private readonly MainWindow mainWindow;
    private readonly MainViewModel viewModel;
    private readonly AppSettings currentSettings;
    private readonly Dispatcher uiDispatcher;
    private readonly CancellationTokenSource cts = new();
    private Task? listenerTask;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public BenchmarkControlService(string pipeName, MainWindow mainWindow, MainViewModel viewModel, AppSettings currentSettings, Dispatcher uiDispatcher)
    {
        this.pipeName = NormalizePipeName(pipeName);
        this.mainWindow = mainWindow;
        this.viewModel = viewModel;
        this.currentSettings = currentSettings;
        this.uiDispatcher = uiDispatcher;
    }

    public void Start()
    {
        listenerTask = Task.Run(() => ListenLoopAsync(cts.Token));
        AppLogger.Info($"[BenchmarkControlService] Listening for benchmark commands on pipe: {pipeName}");
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            cts.Cancel();
            if (listenerTask != null)
            {
                await listenerTask;
            }
        }
        catch { }
        finally
        {
            cts.Dispose();
        }
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(cancellationToken);
                await HandleClientAsync(server, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                AppLogger.Error("[BenchmarkControlService] Error in pipe listener loop:", ex);
                await Task.Delay(200, cancellationToken);
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream server, CancellationToken cancellationToken)
    {
        var masterKey = KeyManager.GetOrCreateMasterKey();

        while (!cancellationToken.IsCancellationRequested && server.IsConnected)
        {
            byte[] incomingFrame;
            try
            {
                incomingFrame = await PipeFrameCodec.ReadFrameAsync(server, PipeFrameCodec.DefaultMaxMessageSize, cancellationToken);
            }
            catch (EndOfStreamException)
            {
                break;
            }
            catch (Exception ex)
            {
                AppLogger.Error("[BenchmarkControlService] Failed to read frame from pipe:", ex);
                break;
            }

            BenchmarkControlCommand? command = null;
            try
            {
                var decrypted = WireEnvelopeCodec.Decrypt(incomingFrame, EncryptionKind.Aes256Gcm, masterKey);
                command = JsonSerializer.Deserialize<BenchmarkControlCommand>(decrypted, JsonOptions);
            }
            catch (CryptographicException ex)
            {
                AppLogger.Error($"[BenchmarkControlService] Rejected unauthorized benchmark command: {ex.Message}");
                var errorResponse = new BenchmarkControlResponse
                {
                    Action = "unauthorized",
                    Success = false,
                    ProcessId = Environment.ProcessId,
                    ErrorMessage = "Authentication failed: invalid encryption or master key mismatch."
                };
                var errBytes = JsonSerializer.SerializeToUtf8Bytes(errorResponse, JsonOptions);
                var errEnvelope = WireEnvelopeCodec.Encrypt(errBytes, EncryptionKind.Aes256Gcm, masterKey);
                try
                {
                    await PipeFrameCodec.WriteFrameAsync(server, errEnvelope, cancellationToken);
                }
                catch { }
                break;
            }
            catch (Exception ex)
            {
                AppLogger.Error("[BenchmarkControlService] Failed to parse command:", ex);
            }

            if (command is null)
            {
                var errorResponse = new BenchmarkControlResponse
                {
                    Action = "unknown",
                    Success = false,
                    ProcessId = Environment.ProcessId,
                    ErrorMessage = "Invalid command format."
                };
                var errBytes = JsonSerializer.SerializeToUtf8Bytes(errorResponse, JsonOptions);
                var errEnvelope = WireEnvelopeCodec.Encrypt(errBytes, EncryptionKind.Aes256Gcm, masterKey);
                await PipeFrameCodec.WriteFrameAsync(server, errEnvelope, cancellationToken);
                continue;
            }

            var response = await ProcessCommandAsync(command, cancellationToken);
            var respBytes = JsonSerializer.SerializeToUtf8Bytes(response, JsonOptions);
            var respEnvelope = WireEnvelopeCodec.Encrypt(respBytes, EncryptionKind.Aes256Gcm, masterKey);
            await PipeFrameCodec.WriteFrameAsync(server, respEnvelope, cancellationToken);

            if (string.Equals(command.Action, "shutdown", StringComparison.OrdinalIgnoreCase))
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100);
                    uiDispatcher.Invoke(() => Application.Current.Shutdown());
                });
                break;
            }
        }
    }

    private async Task<BenchmarkControlResponse> ProcessCommandAsync(BenchmarkControlCommand command, CancellationToken cancellationToken)
    {
        var action = command.Action.ToLowerInvariant();
        try
        {
            return action switch
            {
                "ping" => HandlePing(),
                "search" => await HandleSearchAsync(command, cancellationToken),
                "apply_settings" => HandleApplySettings(command),
                "shutdown" => HandleShutdown(),
                "stress_test_resolver" => await HandleStressTestResolverAsync(cancellationToken),
                "reset_caches" => HandleResetCaches(),
                _ => new BenchmarkControlResponse
                {
                    Action = command.Action,
                    Success = false,
                    ProcessId = Environment.ProcessId,
                    ErrorMessage = $"Unknown action: '{command.Action}'"
                }
            };
        }
        catch (Exception ex)
        {
            AppLogger.Error($"[BenchmarkControlService] Error processing action '{command.Action}':", ex);
            return new BenchmarkControlResponse
            {
                Action = command.Action,
                Success = false,
                ProcessId = Environment.ProcessId,
                ErrorMessage = ex.Message
            };
        }
    }

    private static BenchmarkControlResponse HandlePing()
    {
        return new BenchmarkControlResponse
        {
            Action = "ping",
            Success = true,
            ProcessId = Environment.ProcessId
        };
    }

    private BenchmarkControlResponse HandleResetCaches()
    {
        uiDispatcher.Invoke(() =>
        {
            viewModel.ResetCaches();
            mainWindow.ApplySettings(currentSettings);
        });

        return new BenchmarkControlResponse
        {
            Action = "reset_caches",
            Success = true,
            ProcessId = Environment.ProcessId
        };
    }

    private async Task<BenchmarkControlResponse> HandleStressTestResolverAsync(CancellationToken cancellationToken)
    {
        var metrics = await viewModel.StressTestImageResolverAsync(cancellationToken);
        return new BenchmarkControlResponse
        {
            Action = "stress_test_resolver",
            Success = true,
            ProcessId = Environment.ProcessId,
            Report = new SearchBenchmarkReport
            {
                TotalImageResolutionMs = metrics.TotalResolveTimeMs,
                ImageResolverMetrics = metrics,
                Success = true
            }
        };
    }

    private static BenchmarkControlResponse HandleShutdown()
    {
        return new BenchmarkControlResponse
        {
            Action = "shutdown",
            Success = true,
            ProcessId = Environment.ProcessId
        };
    }

    private async Task<BenchmarkControlResponse> HandleSearchAsync(BenchmarkControlCommand command, CancellationToken cancellationToken)
    {
        var report = await viewModel.ExecuteBenchmarkSearchAsync(command.Query, command.Limit, command.BypassDebounce, cancellationToken);
        report.RunId = command.RunId;

        return new BenchmarkControlResponse
        {
            Action = "search",
            Success = report.Success,
            ProcessId = Environment.ProcessId,
            Report = report,
            ErrorMessage = report.ErrorMessage
        };
    }

    private BenchmarkControlResponse HandleApplySettings(BenchmarkControlCommand command)
    {
        if (command.TransportKind.HasValue)
        {
            currentSettings.Provider.ProviderTransportKind = command.TransportKind.Value;
        }

        if (command.SerializationKind.HasValue)
        {
            currentSettings.Provider.ProviderSerialization = command.SerializationKind.Value;
        }

        if (command.SearchMode.HasValue)
        {
            currentSettings.Provider.SearchMode = command.SearchMode.Value;
        }

        if (command.EncryptionKind.HasValue)
        {
            currentSettings.Provider.Encryption = command.EncryptionKind.Value;
        }

        if (command.ImageResolverKind.HasValue)
        {
            currentSettings.Search.ImageResolverKind = command.ImageResolverKind.Value;
        }

        if (command.UseCache.HasValue)
        {
            currentSettings.Search.UseCache = command.UseCache.Value;
        }

        if (command.TimeoutSeconds.HasValue)
        {
            currentSettings.Provider.ProviderTimeoutSeconds = Math.Max(1, command.TimeoutSeconds.Value);
        }

        if (command.DebounceDelayMs.HasValue)
        {
            currentSettings.Search.ProviderDebounceDelayMs = Math.Max(0, command.DebounceDelayMs.Value);
        }

        uiDispatcher.Invoke(() => mainWindow.ApplySettings(currentSettings));

        return new BenchmarkControlResponse
        {
            Action = "apply_settings",
            Success = true,
            ProcessId = Environment.ProcessId
        };
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
