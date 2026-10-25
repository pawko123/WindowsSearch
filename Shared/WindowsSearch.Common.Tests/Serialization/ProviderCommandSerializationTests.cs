using WindowsSearch.Common.Models;
using WindowsSearch.Common.Serialization;

namespace WindowsSearch.Common.Tests.Serialization;

public class ProviderCommandSerializationTests
{
    [Theory]
    [InlineData(SerializationKind.Json)]
    [InlineData(SerializationKind.Protobuf)]
    [InlineData(SerializationKind.Xml)]
    public void Command_RoundtripsSuccessfully(SerializationKind kind)
    {
        var serializer = MessageSerializerFactory.Create(kind);
        var request = new ProviderSearchRequest
        {
            Query = string.Empty,
            Limit = 0,
            Command = new ProviderCommand
            {
                Action = "rebuild"
            }
        };

        var bytes = serializer.Serialize(request);
        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);

        var deserialized = serializer.Deserialize<ProviderSearchRequest>(bytes);
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.Command);
        Assert.Equal("rebuild", deserialized.Command.Action);
    }

    [Theory]
    [InlineData(SerializationKind.Json)]
    [InlineData(SerializationKind.Protobuf)]
    [InlineData(SerializationKind.Xml)]
    public void CommandResult_RoundtripsSuccessfully(SerializationKind kind)
    {
        var serializer = MessageSerializerFactory.Create(kind);
        var response = new ProviderSearchResponse
        {
            ProviderExecutionMs = 12.5,
            CommandResult = new ProviderCommandResult
            {
                Success = true,
                Message = "Rebuild started",
                IsRunning = true,
                ProgressPercentage = 45.5,
                Details =
                [
                    new CommandDetail("Files", "1024"),
                    new CommandDetail("Speed", "500/s")
                ]
            }
        };

        var bytes = serializer.Serialize(response);
        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);

        var deserialized = serializer.Deserialize<ProviderSearchResponse>(bytes);
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.CommandResult);
        Assert.True(deserialized.CommandResult.Success);
        Assert.Equal("Rebuild started", deserialized.CommandResult.Message);
        Assert.True(deserialized.CommandResult.IsRunning);
        Assert.Equal(45.5, deserialized.CommandResult.ProgressPercentage);
        Assert.Equal(2, deserialized.CommandResult.Details.Count);
        Assert.Equal("Files", deserialized.CommandResult.Details[0].Key);
        Assert.Equal("1024", deserialized.CommandResult.Details[0].Value);
        Assert.Equal("Speed", deserialized.CommandResult.Details[1].Key);
        Assert.Equal("500/s", deserialized.CommandResult.Details[1].Value);
    }

    private sealed class SampleStatusDetails : CommandDetailsBase
    {
        [System.ComponentModel.DataAnnotations.Display(Name = "Files Count", Order = 1)]
        public int Files { get; init; } = 42;

        [System.ComponentModel.DataAnnotations.Display(Name = "Status Text", Order = 2)]
        public string Status { get; init; } = "Active";
    }

    [Fact]
    public void CommandDetailsBase_ToDetails_ExtractsOrderedDisplayProperties()
    {
        var sample = new SampleStatusDetails();
        var details = sample.ToDetails();

        Assert.Equal(2, details.Count);
        Assert.Equal("Files Count", details[0].Key);
        Assert.Equal("42", details[0].Value);
        Assert.Equal("Status Text", details[1].Key);
        Assert.Equal("Active", details[1].Value);
    }
}
