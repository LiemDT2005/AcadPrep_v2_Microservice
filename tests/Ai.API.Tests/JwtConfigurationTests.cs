using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ai.API.Tests;

// Shared BuildingBlocks helper + this service's wrapper: only an RSA public key is accepted.
public class JwtConfigurationTests
{
    [Fact]
    public void PublicKeyIsAccepted()
    {
        using var rsa = RSA.Create(2048);
        using var provider = CreateProvider(rsa.ExportSubjectPublicKeyInfoPem());
        Assert.False(GetOptions(provider).MapInboundClaims);
    }

    [Theory]
    [InlineData("pkcs8")]
    [InlineData("pkcs1")]
    [InlineData("weak")]
    [InlineData("missing-file")]
    public void InvalidKeyFailsClosedWithoutDisclosingIt(string reason)
    {
        using var rsa = RSA.Create(reason == "weak" ? 1024 : 2048);
        var pem = reason switch
        {
            "pkcs8" => rsa.ExportPkcs8PrivateKeyPem(),
            "pkcs1" => rsa.ExportRSAPrivateKeyPem(),
            "weak" => rsa.ExportSubjectPublicKeyInfoPem(),
            _ => null
        };
        using var provider = CreateProvider(pem, reason == "missing-file" ? "missing-public-key.pem" : null);
        var exception = Assert.Throws<OptionsValidationException>(() => GetOptions(provider));
        if (pem is not null) Assert.DoesNotContain(pem, exception.ToString());
        Assert.DoesNotContain("missing-public-key.pem", exception.ToString());
    }

    private static JwtBearerOptions GetOptions(ServiceProvider provider) => provider
        .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);

    private static ServiceProvider CreateProvider(string? pem, string? path = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "ToeicLearning.Identity",
            ["Jwt:Audience"] = "ToeicLearning.Clients",
            ["Jwt:PublicKey"] = pem,
            ["Jwt:PublicKeyPath"] = path
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddJwtAuthentication(configuration, new HostingEnvironment { ContentRootPath = AppContext.BaseDirectory });
        return services.BuildServiceProvider();
    }
}
