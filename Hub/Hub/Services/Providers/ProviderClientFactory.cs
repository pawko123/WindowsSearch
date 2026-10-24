using WindowsSearch.Common.Models;
using Hub.Services.Providers.Transports;
using WindowsSearch.Common.Serialization;
using WindowsSearch.Common.Security;

namespace Hub.Services.Providers;

public static class ProviderClientFactory
{
    public static IProviderClient Create(ProviderTransportKind transport, string endpoint, int timeoutSeconds, IMessageSerializer serializer, EncryptionKind encryptionKind = EncryptionKind.None, byte[]? masterKey = null)
    {
        return transport switch
        {
            ProviderTransportKind.NamedPipe => new NamedPipeProviderClient(endpoint, timeoutSeconds, serializer, encryptionKind, masterKey),
            ProviderTransportKind.Http => new HttpProviderClient(endpoint, timeoutSeconds, serializer, encryptionKind, masterKey),
            ProviderTransportKind.Grpc => new GrpcProviderClient(endpoint, timeoutSeconds, serializer, encryptionKind, masterKey),
            _ => new NamedPipeProviderClient(endpoint, timeoutSeconds, serializer, encryptionKind, masterKey),
        };
    }
}
