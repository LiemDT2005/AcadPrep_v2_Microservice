using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Infrastructure.Persistence;
using Xunit;

namespace Notifications.API.Tests;

public class FoundationTests : IClassFixture<NotificationsApiFactory>
{
    private readonly NotificationsApiFactory factory;

    public FoundationTests(NotificationsApiFactory factory) => this.factory = factory;

    [Fact]
    public async Task Health_IsAnonymousAndDoesNotNeedDatabase()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public void DbContext_IsRegisteredWithSqlServerAndSchemaWithoutOpeningConnection()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", db.Database.ProviderName);
        Assert.Equal(2, db.Model.GetEntityTypes().Count());
        Assert.EndsWith("_InitialNotifications", Assert.Single(db.Database.GetMigrations()));
        Assert.Equal(System.Data.ConnectionState.Closed, db.Database.GetDbConnection().State);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(422)]
    public async Task ErrorStatus_UsesProblemDetails(int status)
    {
        using var client = CreateAuthenticatedClient();
        using var response = await client.GetAsync($"/_foundation-tests/status/{status}");
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(status, json.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task UnknownRoute_ReturnsNotFoundProblemDetails()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task InvalidRequest_UsesFieldValidationProblemDetails()
    {
        using var client = CreateAuthenticatedClient();
        using var response = await client.PostAsJsonAsync("/_foundation-tests/validation", new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("Name", out _));
    }

    [Fact]
    public async Task BusinessValidation_CanReturnFieldErrorsWith422()
    {
        using var client = CreateAuthenticatedClient();
        using var response = await client.PostAsJsonAsync("/_foundation-tests/business-validation", new { });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("audience", out _));
    }

    [Fact]
    public async Task UnexpectedError_DoesNotLeakProductionDiagnostics()
    {
        using var client = CreateAuthenticatedClient();
        using var response = await client.GetAsync("/_foundation-tests/exception");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Internal foundation test diagnostic", body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Swagger_IsNotExposedInProduction()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_InDevelopment_HasBearerAuthorization()
    {
        using var development = new NotificationsApiFactory { EnvironmentName = "Development" };
        using var client = development.CreateClient();
        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var bearer = json.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        Assert.True(json.RootElement.GetProperty("security")[0].TryGetProperty("Bearer", out _));
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken());
        return client;
    }
}
