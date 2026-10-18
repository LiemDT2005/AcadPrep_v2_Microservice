using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Notifications.API.Authentication;
using Xunit;

namespace Notifications.API.Tests;

public class CurrentUserTests
{
    [Fact]
    public void MissingHttpContext_ReturnsAnonymousEmptyIdentity()
    {
        var user = new CurrentUser(new HttpContextAccessor());
        Assert.False(user.IsAuthenticated);
        Assert.Null(user.UserId);
        Assert.Null(user.Email);
        Assert.Empty(user.Roles);
    }

    [Fact]
    public void UnauthenticatedClaims_AreNotTrusted()
    {
        var user = CreateUser(new ClaimsIdentity([
            new Claim("sub", Guid.NewGuid().ToString()), new Claim("email", "untrusted@example.test"),
            new Claim(ClaimTypes.Role, "Admin")]));
        Assert.False(user.IsAuthenticated);
        Assert.Null(user.UserId);
        Assert.Null(user.Email);
        Assert.Empty(user.Roles);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void InvalidSubject_ReturnsNullWithoutThrowing(string? subject)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) };
        if (subject is not null) claims.Add(new Claim("sub", subject));
        var user = CreateUser(new ClaimsIdentity(claims, "test"));
        Assert.True(user.IsAuthenticated);
        Assert.Null(user.UserId);
    }

    [Fact]
    public void Subject_IsCanonicalAndRolesAreDistinct()
    {
        var id = Guid.NewGuid();
        var user = CreateUser(new ClaimsIdentity([
            new Claim("sub", id.ToString()), new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("email", "admin@example.test"), new Claim(ClaimTypes.Role, "Admin"),
            new Claim(ClaimTypes.Role, "Admin"), new Claim(ClaimTypes.Role, "Learner")], "test"));
        Assert.Equal(id, user.UserId);
        Assert.Equal("admin@example.test", user.Email);
        Assert.Equal(new[] { "Admin", "Learner" }, user.Roles);
    }

    private static CurrentUser CreateUser(ClaimsIdentity identity) => new(new HttpContextAccessor
    {
        HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
    });
}
