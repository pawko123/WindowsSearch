using Hub.Models.Settings;
using Hub.Services.Providers;

namespace Hub.Tests.Services.Providers;

public class ProviderCommandServiceTests
{
    private readonly ProviderCommandService _service = new();

    [Fact]
    public async Task ExecuteCommandAsync_NullSettings_ReturnsFailureResult()
    {
        var hubSettings = new AppSettings();
        var result = await _service.ExecuteCommandAsync("rebuild", hubSettings, null, null);

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Provider context not available.", result.Message);
    }
}
