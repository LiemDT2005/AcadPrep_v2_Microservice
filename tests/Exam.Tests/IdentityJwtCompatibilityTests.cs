using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BuildingBlocks.Common.Authentication;
using Identity.API.Models;
using Identity.API.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Exam.Tests;

public class IdentityJwtCompatibilityTests
{
    private static string Root
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ToeicLearning.sln")))
                directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        }
    }

    [Theory]
    [InlineData("Exam")]
    [InlineData("Content")]
    [InlineData("Learning/Learning.API")]
    [InlineData("Catalog/Catalog.API")]
    [InlineData("Progress/Progress.API")]
    public void ServiceAcceptsActualIdentityTokenAndPreservesAdminRole(string service)
    {
        var identityRoot = Path.Combine(Root, "src/Services/Identity/Identity.API");
        var identityConfig = new ConfigurationBuilder().SetBasePath(identityRoot).AddJsonFile("appsettings.json").Build();
        var tokenService = new TokenService(identityConfig, new TestEnvironment(identityRoot), null!);
        var user = new AppUser { Id = "integration-user", Email = "integration@example.test", FullName = "Integration User" };
        var (token, _) = tokenService.CreateToken(user, ["Admin"]);
        var serviceRoot = Path.Combine(Root, "src/Services", service);
        var config = new ConfigurationBuilder().SetBasePath(serviceRoot).AddJsonFile("appsettings.json").Build();
        var parameters = IdentityAuthenticationExtensions.CreateValidationParameters(config, serviceRoot);
        var principal = new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);
        Assert.Equal(user.Id, principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.True(principal.IsInRole("Admin"));
    }

    [Theory]
    [InlineData("wrong-key")]
    [InlineData("symmetric")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    public void ServiceRejectsInvalidIdentityToken(string failure)
    {
        using var trusted = RSA.Create(2048);
        using var other = RSA.Create(2048);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:PublicKey"] = trusted.ExportSubjectPublicKeyInfoPem(),
            ["Jwt:Issuer"] = "identity",
            ["Jwt:Audience"] = "clients"
        }).Build();
        SecurityKey signingKey = failure == "symmetric"
            ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('x', 64)))
            : new RsaSecurityKey(failure == "wrong-key" ? other : trusted) { KeyId = "acadprep-rsa-key-1" };
        var token = new JwtSecurityToken(
            issuer: failure == "issuer" ? "untrusted" : "identity",
            audience: failure == "audience" ? "other" : "clients",
            expires: DateTime.UtcNow.AddMinutes(failure == "expired" ? -10 : 10),
            signingCredentials: new SigningCredentials(signingKey,
                failure == "symmetric" ? SecurityAlgorithms.HmacSha256 : SecurityAlgorithms.RsaSha256));
        var serialized = new JwtSecurityTokenHandler().WriteToken(token);
        var parameters = IdentityAuthenticationExtensions.CreateValidationParameters(config, Root);
        Assert.ThrowsAny<SecurityTokenException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(serialized, parameters, out _));
    }

    private sealed class TestEnvironment(string contentRoot) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Identity.API";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRoot;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
