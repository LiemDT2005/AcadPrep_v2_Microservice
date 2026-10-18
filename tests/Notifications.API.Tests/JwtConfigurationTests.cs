using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Notifications.API.Authentication;
using Xunit;

namespace Notifications.API.Tests;

public class JwtConfigurationTests
{
    [Fact]
    public void InlinePublicKey_IsAcceptedAndValidationCannotBeDisabled()
    {
        using var rsa = RSA.Create(2048);
        using var provider = CreateProvider(rsa.ExportSubjectPublicKeyInfoPem());
        var options = GetOptions(provider);
        Assert.False(options.MapInboundClaims);
        Assert.False(options.IncludeErrorDetails);
        var validation = options.TokenValidationParameters;
        Assert.True(validation.ValidateIssuer);
        Assert.True(validation.ValidateAudience);
        Assert.True(validation.ValidateLifetime);
        Assert.True(validation.ValidateIssuerSigningKey);
        Assert.True(validation.RequireExpirationTime);
        Assert.True(validation.RequireSignedTokens);
        Assert.Equal(new[] { "RS256" }, validation.ValidAlgorithms);
    }

    [Fact]
    public void RelativePublicKeyPath_IsResolvedAgainstContentRoot()
    {
        using var rsa = RSA.Create(2048);
        var filename = $"foundation-public-{Guid.NewGuid():N}.pem";
        var path = Path.Combine(AppContext.BaseDirectory, filename);
        try
        {
            File.WriteAllText(path, rsa.ExportSubjectPublicKeyInfoPem());
            using var provider = CreateProvider(null, filename);
            Assert.NotNull(GetOptions(provider).TokenValidationParameters.IssuerSigningKey);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("unreadable")]
    [InlineData("invalid")]
    [InlineData("private")]
    [InlineData("weak")]
    [InlineData("ambiguous")]
    public void InvalidKeyConfiguration_FailsClosedWithoutDisclosingKey(string reason)
    {
        using var rsa = RSA.Create(reason == "weak" ? 1024 : 2048);
        var pem = reason switch
        {
            "missing" or "unreadable" => null,
            "invalid" => "invalid-public-key",
            "private" => rsa.ExportPkcs8PrivateKeyPem(),
            _ => rsa.ExportSubjectPublicKeyInfoPem()
        };
        using var provider = CreateProvider(pem,
            reason is "unreadable" or "ambiguous" ? "missing-public-key.pem" : null);
        var exception = Assert.Throws<OptionsValidationException>(() => GetOptions(provider));
        if (pem is not null) Assert.DoesNotContain(pem, exception.Message);
        Assert.DoesNotContain("missing-public-key.pem", exception.Message);
    }

    private static JwtBearerOptions GetOptions(ServiceProvider provider) => provider
        .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);

    private static ServiceProvider CreateProvider(string? pem, string? path = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = NotificationsApiFactory.Issuer,
            ["Jwt:Audience"] = NotificationsApiFactory.Audience,
            ["Jwt:KeyId"] = NotificationsApiFactory.KeyId,
            ["Jwt:PublicKey"] = pem,
            ["Jwt:PublicKeyPath"] = path
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IWebHostEnvironment>(new TestEnvironment());
        services.AddNotificationAuthentication(configuration);
        return services.BuildServiceProvider();
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Notifications.API";
        public string EnvironmentName { get; set; } = "Production";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
