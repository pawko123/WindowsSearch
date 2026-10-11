using System.Text.Json;
using WindowsSearch.Common.Models;
using WindowsSearch.Common.Models.Benchmark;
using WindowsSearch.Common.Serialization;

namespace WindowsSearch.Common.Tests.Serialization;

public class BenchmarkModelsSerializationTests
{
    [Fact]
    public void ProviderSearchResponse_WithProviderExecutionMs_SerializesAndDeserializes_Json()
    {
        var serializer = new JsonMessageSerializer();
        var original = new ProviderSearchResponse
        {
            ProviderExecutionMs = 42.5,
            Categories =
            [
                new ProviderResultCategory
                {
                    Name = "Test Category",
                    Items = [new ProviderResultItem { Title = "Item 1" }]
                }
            ]
        };

        var bytes = serializer.Serialize(original);
        var restored = serializer.Deserialize<ProviderSearchResponse>(bytes);

        Assert.NotNull(restored);
        Assert.Equal(42.5, restored.ProviderExecutionMs);
        Assert.Single(restored.Categories);
        Assert.Equal("Test Category", restored.Categories[0].Name);
    }

    [Fact]
    public void ProviderSearchResponse_WithProviderExecutionMs_SerializesAndDeserializes_Protobuf()
    {
        var serializer = new ProtobufMessageSerializer();
        var original = new ProviderSearchResponse
        {
            ProviderExecutionMs = 123.45,
            Categories =
            [
                new ProviderResultCategory
                {
                    Name = "Proto Category",
                    Items = [new ProviderResultItem { Title = "Proto Item" }]
                }
            ]
        };

        var bytes = serializer.Serialize(original);
        var restored = serializer.Deserialize<ProviderSearchResponse>(bytes);

        Assert.NotNull(restored);
        Assert.Equal(123.45, restored.ProviderExecutionMs);
        Assert.Single(restored.Categories);
        Assert.Equal("Proto Category", restored.Categories[0].Name);
    }

    [Fact]
    public void ProviderSearchResponse_WithProviderExecutionMs_SerializesAndDeserializes_Xml()
    {
        var serializer = new XmlMessageSerializer();
        var original = new ProviderSearchResponse
        {
            ProviderExecutionMs = 78.9,
            Categories =
            [
                new ProviderResultCategory
                {
                    Name = "Xml Category",
                    Items = [new ProviderResultItem { Title = "Xml Item" }]
                }
            ]
        };

        var bytes = serializer.Serialize(original);
        var restored = serializer.Deserialize<ProviderSearchResponse>(bytes);

        Assert.NotNull(restored);
        Assert.Equal(78.9, restored.ProviderExecutionMs);
        Assert.Single(restored.Categories);
        Assert.Equal("Xml Category", restored.Categories[0].Name);
    }

    [Fact]
    public void BenchmarkControlCommand_SerializesAndDeserializes_Json()
    {
        var command = new BenchmarkControlCommand
        {
            Action = "search",
            RunId = "run_101",
            Query = "git status",
            Limit = 50,
            BypassDebounce = true,
            TransportKind = "NamedPipe",
            SerializationKind = "Json",
            SearchMode = "ConcurrentStream",
            ImageResolverKind = "ConcurrentDictionary",
            UseCache = true,
            TimeoutSeconds = 10,
            DebounceDelayMs = 0
        };

        var json = JsonSerializer.Serialize(command);
        var restored = JsonSerializer.Deserialize<BenchmarkControlCommand>(json);

        Assert.NotNull(restored);
        Assert.Equal("search", restored.Action);
        Assert.Equal("run_101", restored.RunId);
        Assert.Equal("git status", restored.Query);
        Assert.Equal(50, restored.Limit);
        Assert.True(restored.BypassDebounce);
        Assert.Equal("NamedPipe", restored.TransportKind);
        Assert.Equal("Json", restored.SerializationKind);
        Assert.Equal("ConcurrentStream", restored.SearchMode);
        Assert.Equal("ConcurrentDictionary", restored.ImageResolverKind);
        Assert.True(restored.UseCache);
        Assert.Equal(10, restored.TimeoutSeconds);
        Assert.Equal(0, restored.DebounceDelayMs);
    }

    [Fact]
    public void SearchBenchmarkReport_SerializesAndDeserializes_Json()
    {
        var report = new SearchBenchmarkReport
        {
            RunId = "run_42",
            Query = "calc",
            TotalWallClockMs = 15.2,
            TransportKind = "NamedPipe",
            SerializationKind = "Protobuf",
            SearchMode = "ConcurrentBlocking",
            ProviderTimeoutSeconds = 5,
            TotalAggregationMs = 1.1,
            TotalImageResolutionMs = 2.4,
            ProviderMetrics =
            [
                new ProviderBenchmarkMetrics
                {
                    ProviderName = "DemoProvider",
                    RequestSerializationMs = 0.3,
                    RequestPayloadBytes = 120,
                    TransportRoundtripMs = 8.5,
                    ProviderExecutionMs = 7.1,
                    ResponseDeserializationMs = 0.4,
                    ResponsePayloadBytes = 450,
                    AggregationMs = 0.2,
                    ItemCount = 5,
                    CategoryCount = 1,
                    Success = true
                }
            ],
            ImageResolverMetrics = new ImageResolverBenchmarkMetrics
            {
                ResolverKind = "MemoryCacheImageResolver",
                UseCache = true,
                ImagesRequested = 6,
                CacheHits = 4,
                CacheMisses = 2,
                TotalResolveTimeMs = 2.4
            },
            Success = true
        };

        var json = JsonSerializer.Serialize(report);
        var restored = JsonSerializer.Deserialize<SearchBenchmarkReport>(json);

        Assert.NotNull(restored);
        Assert.Equal("run_42", restored.RunId);
        Assert.Equal(15.2, restored.TotalWallClockMs);
        Assert.Single(restored.ProviderMetrics);
        Assert.Equal("DemoProvider", restored.ProviderMetrics[0].ProviderName);
        Assert.Equal(8.5, restored.ProviderMetrics[0].TransportRoundtripMs);
        Assert.Equal(6, restored.ImageResolverMetrics.ImagesRequested);
        Assert.Equal(4, restored.ImageResolverMetrics.CacheHits);
        Assert.Equal(2, restored.ImageResolverMetrics.CacheMisses);
    }
}
