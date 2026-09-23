using SupportAgent.Core.DTOs;

namespace SupportAgent.Tests;

public class HealthResponseTests
{
    [Fact]
    public void HealthResponse_StoresValues()
    {
        var response = new HealthResponse("ok", "SupportAgent.NET");

        Assert.Equal("ok", response.Status);
        Assert.Equal("SupportAgent.NET", response.Application);
    }
}
