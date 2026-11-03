using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Notifications.API.Tests;

public class AuthenticationTests : IClassFixture<NotificationsApiFactory>
{
    private readonly NotificationsApiFactory factory;
    private readonly HttpClient client;

    public AuthenticationTests(NotificationsApiFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task ValidIdentityToken_ReadsCurrentUserAndRoles()
    {
        using var response = await GetProtectedAsync(factory.CreateToken());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("isAuthenticated").GetBoolean());
        Assert.Equal(factory.UserId, json.RootElement.GetProperty("userId").GetGuid());
        Assert.Equal("learner@example.test", json.RootElement.GetProperty("email").GetString());
        Assert.Equal("Learner", json.RootElement.GetProperty("roles")[0].GetString());
    }

    [Fact]
    public async Task MissingToken_ReturnsUnauthorizedProblemDetails()
    {
        using var response = await GetProtectedAsync(null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("signature")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    [InlineData("ps256")]
    [InlineData("unsigned")]
    [InlineData("hs256")]
    [InlineData("malformed")]
    public async Task InvalidTokens_AreRejected(string reason)
    {
        using var otherKey = RSA.Create(2048);
        var token = reason switch
        {
            "signature" => factory.CreateToken(key: otherKey),
            "issuer" => factory.CreateToken(issuer: "untrusted-issuer"),
            "audience" => factory.CreateToken(audience: "other-client"),
            "expired" => factory.CreateToken(expires: DateTime.UtcNow.AddSeconds(-1)),
            "ps256" => factory.CreateToken(algorithm: SecurityAlgorithms.RsaSsaPssSha256),
            "unsigned" => factory.CreateToken(algorithm: SecurityAlgorithms.None),
            "hs256" => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
                issuer: NotificationsApiFactory.Issuer, audience: NotificationsApiFactory.Audience,
                claims: [new Claim("sub", factory.UserId.ToString())], expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)),
                    SecurityAlgorithms.HmacSha256))),
            _ => "not-a-jwt"
        };
        using var response = await GetProtectedAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(token, body);
        Assert.DoesNotContain("IDX", body);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task MissingOrInvalidSubject_IsRejectedWithoutIdentifierFallback(string? subject)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, factory.UserId.ToString()) };
        if (subject is not null) claims.Add(new Claim("sub", subject));
        using var response = await GetProtectedAsync(factory.CreateToken(claims: claims));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DuplicateSubjects_AreRejected()
    {
        using var response = await GetProtectedAsync(factory.CreateToken(claims:
            [new Claim("sub", factory.UserId.ToString()), new Claim("sub", Guid.NewGuid().ToString())]));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Admin", HttpStatusCode.NoContent)]
    [InlineData("Learner", HttpStatusCode.Forbidden)]
    [InlineData("Moderator", HttpStatusCode.Forbidden)]
    public async Task IdentityRoleClaim_EnforcesAdminAuthorization(string role, HttpStatusCode expected)
    {
        using var response = await GetProtectedAsync(factory.CreateToken(claims:
            [new Claim("sub", factory.UserId.ToString()), new Claim(ClaimTypes.Role, role)]), "admin");
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.Forbidden)
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    private async Task<HttpResponseMessage> GetProtectedAsync(string? token, string endpoint = "me")
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/_foundation-tests/{endpoint}");
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }
}
