using System.Diagnostics;
using WindowsSearch.Common.Logging;
using WindowsSearch.Common.Pipes;
using WindowsSearch.Common.Security;
using WindowsSearch.Common.Serialization;

namespace BaseProvider.Transports;

internal static class TransportMessageCodec
{
    private static readonly AsyncLocal<double> _lastDecryptionMs = new();
    private static IMessageSerializer _serializer = new JsonMessageSerializer();
    private static EncryptionKind _encryptionKind = EncryptionKind.Aes256Gcm;
    private static byte[]? _masterKey;

    public static string ContentType => _serializer.ContentType;

    public static EncryptionKind EncryptionKind => _encryptionKind;

    public static double LastDecryptionMs
    {
        get => _lastDecryptionMs.Value;
        set => _lastDecryptionMs.Value = value;
    }

    public static void Initialize(IMessageSerializer serializer)
    {
        _serializer = serializer;
        _encryptionKind = EncryptionKind.None;
        _masterKey = null;
    }

    public static void Initialize(IMessageSerializer serializer, EncryptionKind encryptionKind, byte[] masterKey)
    {
        _serializer = serializer;
        _encryptionKind = encryptionKind;
        _masterKey = masterKey;
    }

    public static byte[] Serialize<T>(T value)
    {
        var bytes = _serializer.Serialize(value);

        if (_encryptionKind != EncryptionKind.None)
        {
            var envelope = WireEnvelopeCodec.Encrypt(bytes, _encryptionKind, _masterKey!);
            if (AppLogger.LogLevel <= LogLevel.Debug)
            {
                AppLogger.Debug($"[Encrypted outgoing payload] {WireEnvelopeCodec.FormatEnvelopeForLog(envelope, _encryptionKind)}");
            }
            return envelope;
        }

        return WireEnvelopeCodec.Encrypt(bytes, EncryptionKind.None, Array.Empty<byte>());
    }

    public static T Deserialize<T>(byte[] bytes)
    {
        if (_encryptionKind != EncryptionKind.None)
        {
            if (AppLogger.LogLevel <= LogLevel.Debug)
            {
                AppLogger.Debug($"[Encrypted incoming payload] {WireEnvelopeCodec.FormatEnvelopeForLog(bytes, _encryptionKind)}");
            }

            var sw = Stopwatch.StartNew();
            var decrypted = WireEnvelopeCodec.Decrypt(bytes, _encryptionKind, _masterKey!);
            sw.Stop();
            LastDecryptionMs = sw.Elapsed.TotalMilliseconds;

            return _serializer.Deserialize<T>(decrypted) ?? throw new InvalidOperationException("Unable to deserialize provider message.");
        }

        LastDecryptionMs = 0;
        if (bytes.Length >= 2 && bytes[0] == WireEnvelopeCodec.CurrentVersion)
        {
            var decrypted = WireEnvelopeCodec.Decrypt(bytes, EncryptionKind.None, Array.Empty<byte>());
            return _serializer.Deserialize<T>(decrypted) ?? throw new InvalidOperationException("Unable to deserialize provider message.");
        }

        return _serializer.Deserialize<T>(bytes) ?? throw new InvalidOperationException("Unable to deserialize provider message.");
    }

    public static Task WriteFrameAsync(Stream stream, byte[] payload, CancellationToken cancellationToken)
    {
        return PipeFrameCodec.WriteFrameAsync(stream, payload, cancellationToken);
    }

    public static Task<byte[]> ReadFrameAsync(Stream stream, CancellationToken cancellationToken)
    {
        return PipeFrameCodec.ReadFrameAsync(stream, PipeFrameCodec.DefaultMaxMessageSize, cancellationToken);
    }
}
