using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using Hub.Models.Settings;
using WindowsSearch.Common.Logging;
using WindowsSearch.Common.Models;
using WindowsSearch.Common.Security;
using WindowsSearch.Common.Serialization;

namespace Hub.Services.Providers;

public sealed class ProviderCommandService
{
    private static readonly ConcurrentDictionary<string, Process> RunningProcesses = new(StringComparer.OrdinalIgnoreCase);

    public async Task<ProviderCommandResult> ExecuteCommandAsync(
        string actionName,
        AppSettings hubSettings,
        ProviderSettingsBase? providerSettings,
        ProviderSettingsModel? providerModel,
        CancellationToken cancellationToken = default)
    {
        if (providerSettings == null || providerModel == null)
        {
            return new ProviderCommandResult
            {
                Success = false,
                Message = "Provider context not available."
            };
        }

        var transportKind = hubSettings.Provider.ProviderTransportKind;
        var endpoint = transportKind switch
        {
            ProviderTransportKind.Http => providerSettings.Endpoints.Http,
            ProviderTransportKind.Grpc => providerSettings.Endpoints.Grpc,
            _ => providerSettings.Endpoints.NamedPipe
        };

        var serialization = hubSettings.Provider.ProviderSerialization;
        var serializer = MessageSerializerFactory.Create(serialization);
        var encryption = hubSettings.Provider.Encryption;
        var masterKey = KeyManager.GetOrCreateMasterKey();
        var timeoutSeconds = Math.Max(30, hubSettings.Provider.ProviderTimeoutSeconds);

        var request = new ProviderSearchRequest
        {
            Query = string.Empty,
            Limit = 0,
            SettingsYaml = ProviderSettingsYaml.Serialize(providerSettings),
            TimeoutMs = timeoutSeconds * 1000,
            Command = new ProviderCommand
            {
                Action = actionName
            }
        };

        try
        {
            AppLogger.Info($"[ProviderCommandService] Dispatching command '{actionName}' to provider '{providerModel.ProviderName}' at '{endpoint}' ({transportKind})...");
            await using var client = ProviderClientFactory.Create(transportKind, endpoint, timeoutSeconds, serializer, encryption, masterKey);
            var response = await client.SearchAsync(request, cancellationToken);
            var cmdResult = response.CommandResult ?? new ProviderCommandResult
            {
                Success = true,
                Message = "Command executed without result payload."
            };
            AppLogger.Info($"[ProviderCommandService] Command '{actionName}' on '{providerModel.ProviderName}' response: Success={cmdResult.Success}, IsRunning={cmdResult.IsRunning}, Message='{cmdResult.Message}'");
            return cmdResult;
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"[ProviderCommandService] Initial connection to provider '{providerModel.ProviderName}' at '{endpoint}' failed: {ex.Message}. Checking if executable exists...");

            var providerDirectory = Path.GetDirectoryName(providerModel.SettingsPath) ?? AppContext.BaseDirectory;
            var executablePath = Path.Combine(providerDirectory, $"{providerModel.ProviderName}.exe");
            if (File.Exists(executablePath))
            {
                try
                {
                    if (!RunningProcesses.TryGetValue(providerModel.ProviderName, out var proc) || proc.HasExited)
                    {
                        proc?.Dispose();
                        AppLogger.Info($"[ProviderCommandService] Spawning provider process '{executablePath}'...");
                        proc = Process.Start(new ProcessStartInfo
                        {
                            FileName = executablePath,
                            Arguments = $"{transportKind} \"{endpoint}\" {serialization} {encryption}",
                            WorkingDirectory = providerDirectory,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden,
                            RedirectStandardInput = true
                        });

                        if (proc != null)
                        {
                            RunningProcesses[providerModel.ProviderName] = proc;
                        }
                    }

                    await Task.Delay(750, cancellationToken);
                    await using var retryClient = ProviderClientFactory.Create(transportKind, endpoint, timeoutSeconds, serializer, encryption, masterKey);
                    var retryResponse = await retryClient.SearchAsync(request, cancellationToken);
                    var retryResult = retryResponse.CommandResult ?? new ProviderCommandResult
                    {
                        Success = true,
                        Message = "Command executed successfully after starting provider."
                    };
                    AppLogger.Info($"[ProviderCommandService] Command '{actionName}' on '{providerModel.ProviderName}' succeeded after auto-start: {retryResult.Message}");
                    return retryResult;
                }
                catch (Exception launchEx)
                {
                    AppLogger.Error($"[ProviderCommandService] Failed to start provider '{providerModel.ProviderName}':", launchEx);
                    return new ProviderCommandResult
                    {
                        Success = false,
                        Message = $"Provider was not running and could not be started: {launchEx.Message}"
                    };
                }
            }

            AppLogger.Error($"[ProviderCommandService] Provider '{providerModel.ProviderName}' is not running and executable '{executablePath}' was not found.");
            return new ProviderCommandResult
            {
                Success = false,
                Message = $"Cannot connect to provider '{providerModel.ProviderName}' at '{endpoint}'."
            };
        }
    }
}
