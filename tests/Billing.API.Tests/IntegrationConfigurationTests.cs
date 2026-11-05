using System.Text.Json;
using Xunit;

namespace Billing.API.Tests;

public class IntegrationConfigurationTests
{
    [Fact]
    public void GatewayStripsOnlyBillingPrefixAndKeepsNotificationRoute()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "src/ApiGateways/ApiGateway/appsettings.json")));
        var proxy = json.RootElement.GetProperty("ReverseProxy");
        var route = proxy.GetProperty("Routes").GetProperty("billing-route");
        Assert.Equal("/billing/{**catch-all}", route.GetProperty("Match").GetProperty("Path").GetString());
        Assert.Equal("/{**catch-all}", Assert.Single(route.GetProperty("Transforms").EnumerateArray()).GetProperty("PathPattern").GetString());
        Assert.Equal("http://localhost:5011/", proxy.GetProperty("Clusters").GetProperty("billing").GetProperty("Destinations").GetProperty("d1").GetProperty("Address").GetString());
        Assert.True(proxy.GetProperty("Routes").TryGetProperty("notification-route", out _));
    }

    [Fact]
    public void ComposeUsesBillingDatabaseAndContainerDns()
    {
        // Same pattern as Notification and AI: optional override, default to Billing's own database on the container.
        var compose = File.ReadAllText(Path.Combine(Root(), "docker-compose.yml"));
        Assert.Contains("${BILLING_CONNECTION_STRING:?", compose);
        Assert.Contains("ReverseProxy__Clusters__billing__Destinations__d1__Address=http://billing.api:8080/", compose);
        var dockerIgnore = File.ReadAllText(Path.Combine(Root(), "src/Services/Billing/Billing.API/Dockerfile.dockerignore"));
        Assert.Contains("**/*.pem", dockerIgnore);
        Assert.Contains("**/bin/", dockerIgnore);
    }

    private static string Root()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "ToeicLearning.sln"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
