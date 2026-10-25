using System.Diagnostics;
using BaseProvider.Abstractions;
using WindowsSearch.Common.Models;
using WindowsSearch.Common.Serialization;
using WindowsSearch.Common.Validation;
using BaseProvider.Transports;

using WindowsSearch.Common.Logging;
using WindowsSearch.Common.Security;

namespace BaseProvider.Host;

public static class BaseProviderHost
{
    public static async Task RunAsync<TSettings>(string[] args, IResultFinder<TSettings> resultFinder)
        where TSettings : ProviderSettingsBase, new()
    {
        var providerName = new DirectoryInfo(AppContext.BaseDirectory).Name;
        if (string.IsNullOrWhiteSpace(providerName) || providerName.Equals("bin", StringComparison.OrdinalIgnoreCase))
        {
            providerName = "UnknownProvider";
        }
        var settings = LoadSettings<TSettings>();
        AppLogger.Initialize(providerName, settings.LogLevel);
        AppLogger.Info($"Provider {providerName} starting up...");

        var validationErrors = SettingsValidationHelper.Validate(settings);
        if (validationErrors.Count > 0)
        {
            AppLogger.Error($"Provider {providerName} has invalid settings.yaml: {string.Join("; ", validationErrors)}");
            throw new InvalidOperationException($"Invalid settings.yaml for provider {providerName}: {string.Join("; ", validationErrors)}");
        }

        try
        {
            var transportKind = ProviderTransportKind.NamedPipe;
            if (args.Length > 0 && Enum.TryParse<ProviderTransportKind>(args[0], true, out var tk))
            {
                transportKind = tk;
            }

            var endpoint = transportKind switch
            {
                ProviderTransportKind.Http => settings.Endpoints.Http,
                ProviderTransportKind.Grpc => settings.Endpoints.Grpc,
                _ => settings.Endpoints.NamedPipe
            };

            if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
            {
                endpoint = args[1];
            }

            var serializationKind = SerializationKind.Json;
            if (args.Length > 2 && Enum.TryParse<SerializationKind>(args[2], true, out var sk))
            {
                serializationKind = sk;
            }

            var encryptionKind = EncryptionKind.Aes256Gcm;
            if (args.Length > 3 && Enum.TryParse<EncryptionKind>(args[3], true, out var ek))
            {
                encryptionKind = ek;
            }

            var masterKey = KeyManager.GetOrCreateMasterKey();

            AppLogger.Info($"Provider {providerName} initialized. Transport: {transportKind}, Endpoint: {endpoint}, Serialization: {serializationKind}, Encryption: {encryptionKind}");

            var serializer = MessageSerializerFactory.Create(serializationKind);
            TransportMessageCodec.Initialize(serializer, encryptionKind, masterKey);
            IProviderTransport transport = new ProviderTransportFactory().Create(transportKind, endpoint);

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            _ = MonitorParentProcessAsync(cts, providerName);

            await transport.RunAsync(async (request, token) =>
            {
                if (AppLogger.LogLevel <= LogLevel.Debug)
                {
                    AppLogger.Debug($"Received request: {serializer.FormatForLog(request)}");
                }

                var effectiveSettings = ResolveEffectiveSettings(settings, request.SettingsYaml, providerName);

                if (request.Command != null)
                {
                    AppLogger.Info($"[BaseProviderHost] Executing command '{request.Command.Action}' for provider '{providerName}'...");
                    var swCmd = Stopwatch.StartNew();
                    ProviderCommandResult cmdResult;
                    if (resultFinder is ICommandHandler<TSettings> commandHandler)
                    {
                        cmdResult = await commandHandler.ExecuteCommandAsync(request.Command, effectiveSettings, token);
                    }
                    else
                    {
                        cmdResult = new ProviderCommandResult
                        {
                            Success = false,
                            Message = "Provider does not support commands."
                        };
                    }
                    swCmd.Stop();

                    if (cmdResult.Success)
                    {
                        AppLogger.Info($"[BaseProviderHost] Command '{request.Command.Action}' completed successfully in {swCmd.ElapsedMilliseconds} ms (IsRunning={cmdResult.IsRunning}): {cmdResult.Message}");
                    }
                    else
                    {
                        AppLogger.Warn($"[BaseProviderHost] Command '{request.Command.Action}' failed after {swCmd.ElapsedMilliseconds} ms: {cmdResult.Message}");
                    }

                    var cmdResponse = new ProviderSearchResponse
                    {
                        CommandResult = cmdResult,
                        ProviderExecutionMs = swCmd.Elapsed.TotalMilliseconds,
                        ProviderDecryptionMs = TransportMessageCodec.LastDecryptionMs
                    };

                    if (encryptionKind != EncryptionKind.None)
                    {
                        var sampleBytes = serializer.Serialize(cmdResponse);
                        var swEnc = Stopwatch.StartNew();
                        _ = WireEnvelopeCodec.Encrypt(sampleBytes, encryptionKind, masterKey);
                        swEnc.Stop();
                        cmdResponse.ProviderEncryptionMs = swEnc.Elapsed.TotalMilliseconds;
                    }
                    else
                    {
                        cmdResponse.ProviderEncryptionMs = 0;
                    }

                    if (AppLogger.LogLevel <= LogLevel.Debug)
                    {
                        AppLogger.Debug($"Sending command response: {serializer.FormatForLog(cmdResponse)}");
                    }

                    return cmdResponse;
                }
                var stopwatch = Stopwatch.StartNew();
                var response = await resultFinder.FindAsync(request, effectiveSettings, token);
                stopwatch.Stop();
                response.ProviderExecutionMs = stopwatch.Elapsed.TotalMilliseconds;
                response.ProviderDecryptionMs = TransportMessageCodec.LastDecryptionMs;
                if (encryptionKind != EncryptionKind.None)
                {
                    var sampleBytes = serializer.Serialize(response);
                    var swEnc = Stopwatch.StartNew();
                    _ = WireEnvelopeCodec.Encrypt(sampleBytes, encryptionKind, masterKey);
                    swEnc.Stop();
                    response.ProviderEncryptionMs = swEnc.Elapsed.TotalMilliseconds;
                }
                else
                {
                    response.ProviderEncryptionMs = 0;
                }
                if (AppLogger.LogLevel <= LogLevel.Debug)
                {
                    AppLogger.Debug($"Sending response: {serializer.FormatForLog(response)}");
                }

                return response;
            }, cts.Token);
        }
        catch (OperationCanceledException)
        {
            AppLogger.Info($"Provider {providerName} shutting down.");
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Provider {providerName} encountered a fatal error during startup:", ex);
            throw;
        }
    }

    private static async Task MonitorParentProcessAsync(CancellationTokenSource cts, string providerName)
    {
        try
        {
            await using var stdin = Console.OpenStandardInput();
            var buffer = new byte[1];
            while (await stdin.ReadAsync(buffer, cts.Token) > 0)
            {
                // Discard any unexpected input; only EOF (0) means the parent's pipe handle closed.
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch
        {
            return;
        }

        AppLogger.Warn($"Provider {providerName} detected that its parent process is no longer available. Shutting down.");
        cts.Cancel();
    }

    private static TSettings LoadSettings<TSettings>() where TSettings : ProviderSettingsBase, new()
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "settings.yaml");
        return ProviderSettingsYaml.Load<TSettings>(settingsPath);
    }

    /// <summary>
    /// Settings Hub sends per-request (so edits apply without restarting the provider) are only
    /// trusted once they pass the same validation as settings.yaml at startup; otherwise this
    /// falls back to the already-validated settings loaded when the process started.
    /// </summary>
    private static TSettings ResolveEffectiveSettings<TSettings>(TSettings startupSettings, string? requestSettingsYaml, string providerName)
        where TSettings : ProviderSettingsBase, new()
    {
        var parsed = ProviderSettingsYaml.Parse<TSettings>(requestSettingsYaml);
        if (parsed is null)
        {
            return startupSettings;
        }

        var errors = SettingsValidationHelper.Validate(parsed);
        if (errors.Count > 0)
        {
            AppLogger.Warn($"Provider {providerName} received invalid settings on request: {string.Join("; ", errors)}. Falling back to settings.yaml.");
            return startupSettings;
        }

        return parsed;
    }
}
