using WindowsSearch.Common.Models;
using WindowsSearch.Common.Models.Benchmark;
using WindowsSearch.Common.Serialization;
using WindowsSearch.Common.Security;

namespace WindowsSearch.Benchmark.Core.Models;

public sealed class SingleRunBenchmarkResult
{
    public string RunId { get; set; } = string.Empty;
    public int Iteration { get; set; }
    public bool IsWarmup { get; set; }
    public ProviderTransportKind Transport { get; set; }
    public SerializationKind Serialization { get; set; }
    public ProviderSearchMode SearchMode { get; set; }
    public EncryptionKind Encryption { get; set; } = EncryptionKind.None;
    public ImageResolverKind ImageResolver { get; set; }
    public bool UseCache { get; set; }
    public string Query { get; set; } = string.Empty;
    public SearchBenchmarkReport? Report { get; set; }
    public SystemResourceSample? ResourceSample { get; set; }
}
