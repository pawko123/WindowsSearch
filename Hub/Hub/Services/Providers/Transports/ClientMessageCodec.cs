using System.Diagnostics;
using WindowsSearch.Common.Logging;
using WindowsSearch.Common.Models.Benchmark;
using WindowsSearch.Common.Security;
using WindowsSearch.Common.Serialization;

namespace Hub.Services.Providers.Transports;

public static class ClientMessageCodec
{
    public static byte[] EncodeRequest<T>(
        T request,
        IMessageSerializer serializer,
        EncryptionKind encryptionKind,
        byte[] masterKey,
        ProviderBenchmarkMetrics? metrics = null,
        string? clientLogTag = null)
    {
        var swSer = Stopwatch.StartNew();
        var data = serializer.Serialize(request);
        swSer.Stop();

        if (metrics != null)
        {
            metrics.RequestSerializationMs = swSer.Elapsed.TotalMilliseconds;
        }

        var swEnc = Stopwatch.StartNew();
        var wireBytes = WireEnvelopeCodec.Encrypt(data, encryptionKind, masterKey);
        swEnc.Stop();

        if (metrics != null)
        {
            metrics.RequestEncryptionMs = swEnc.Elapsed.TotalMilliseconds;
            metrics.RequestPayloadBytes = wireBytes.Length;
        }

        if (AppLogger.LogLevel <= LogLevel.Debug && encryptionKind != EncryptionKind.None)
        {
            var prefix = string.IsNullOrEmpty(clientLogTag) ? "[ClientMessageCodec]" : $"[{clientLogTag}]";
            AppLogger.Debug($"{prefix} Sending {WireEnvelopeCodec.FormatEnvelopeForLog(wireBytes, encryptionKind)}");
        }

        return wireBytes;
    }

    public static T DecodeResponse<T>(
        byte[] wireBytes,
        IMessageSerializer serializer,
        EncryptionKind encryptionKind,
        byte[] masterKey,
        ProviderBenchmarkMetrics? metrics = null,
        string? clientLogTag = null)
    {
        if (metrics != null)
        {
            metrics.ResponsePayloadBytes = wireBytes.Length;
        }

        if (AppLogger.LogLevel <= LogLevel.Debug && encryptionKind != EncryptionKind.None)
        {
            var prefix = string.IsNullOrEmpty(clientLogTag) ? "[ClientMessageCodec]" : $"[{clientLogTag}]";
            AppLogger.Debug($"{prefix} Received {WireEnvelopeCodec.FormatEnvelopeForLog(wireBytes, encryptionKind)}");
        }

        var swDec = Stopwatch.StartNew();
        var decrypted = WireEnvelopeCodec.Decrypt(wireBytes, encryptionKind, masterKey);
        swDec.Stop();

        if (metrics != null)
        {
            metrics.ResponseDecryptionMs = swDec.Elapsed.TotalMilliseconds;
        }

        var swDeser = Stopwatch.StartNew();
        var result = serializer.Deserialize<T>(decrypted) ?? throw new InvalidOperationException("Provider returned an empty or invalid payload.");
        swDeser.Stop();

        if (metrics != null)
        {
            metrics.ResponseDeserializationMs = swDeser.Elapsed.TotalMilliseconds;
        }

        return result;
    }
}
