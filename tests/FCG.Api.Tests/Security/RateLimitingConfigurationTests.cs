using System.Net;
using FCG.Api.Security;
using Microsoft.AspNetCore.Http;

namespace FCG.Api.Tests.Security;

public sealed class RateLimitingConfigurationTests
{
    // Os literais são a especificação do §9.4 do refinamento. Sem isto, alterar os defaults para
    // qualquer valor — inclusive desligar o limite na prática — mantém a suíte inteira verde.
    [Fact]
    public void Defaults_MatchTheDocumentedProductionLimit()
    {
        var options = new RateLimitingOptions();

        Assert.Equal(10, options.PermitLimit);
        Assert.Equal(60, options.WindowSeconds);
    }

    // Sob TestServer o RemoteIpAddress é sempre nulo, então nenhum teste de integração exercita a
    // partição por IP: trocá-la por uma constante global passaria despercebido.
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
