using System.Net;
using FCG.Api.Security;
using Microsoft.AspNetCore.Http;

namespace FCG.Api.Tests.Security;

public sealed class RateLimitingConfigurationTests
{
    [Fact]
    public void Defaults_MatchTheDocumentedProductionLimit()
    {
        var options = new RateLimitingOptions();

        Assert.Equal(10, options.PermitLimit);
        Assert.Equal(60, options.WindowSeconds);
    }

    [Fact]
    public void PartitionKey_SeparatesDistinctRemoteAddresses()
    {
        var first = RateLimitingConfiguration.ResolvePartitionKey(ContextFrom("203.0.113.7"));
        var second = RateLimitingConfiguration.ResolvePartitionKey(ContextFrom("198.51.100.4"));

        Assert.Equal("203.0.113.7", first);
        Assert.Equal("198.51.100.4", second);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void PartitionKey_FallsBackWhenTheRemoteAddressIsUnknown()
    {
        Assert.Equal("unknown", RateLimitingConfiguration.ResolvePartitionKey(ContextFrom(null)));
    }

    private static HttpContext ContextFrom(string? remoteIpAddress)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress =
            remoteIpAddress is null ? null : IPAddress.Parse(remoteIpAddress);

        return context;
    }
}
