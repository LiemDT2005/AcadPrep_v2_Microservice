using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Notifications.API.Tests;

public class GatewayConfigurationTests
{
    [Theory]
    [InlineData(false, "http://localhost:5010/")]
    [InlineData(true, "http://notifications.api:8080/")]
    public void NotificationDestination_ResolvesLocalAndComposeEnvironment(bool docker, string expected)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ToeicLearning.sln"))) root = root.Parent;
        Assert.NotNull(root);
        const string key = "ReverseProxy__Clusters__notification__Destinations__d1__Address";
        var prefix = $"NOTIFICATION_CONFIG_TEST_{Guid.NewGuid():N}_";
        var variable = prefix + key;
        try
        {
            if (docker)
            {
                var line = Assert.Single(File.ReadLines(Path.Combine(root.FullName, "docker-compose.yml")),
                    line => line.TrimStart().StartsWith($"- {key}=", StringComparison.Ordinal));
                Environment.SetEnvironmentVariable(variable, line[(line.IndexOf('=') + 1)..].Trim());
            }
            var config = new ConfigurationBuilder()
                .AddJsonFile(Path.Combine(root.FullName, "src/ApiGateways/ApiGateway/appsettings.json"))
                .AddEnvironmentVariables(prefix).Build();
            using var configLifetime = config as IDisposable;
            var local = new ConfigurationBuilder()
                .AddJsonFile(Path.Combine(root.FullName, "src/ApiGateways/ApiGateway/appsettings.json")).Build();
            using var localLifetime = local as IDisposable;

            Assert.Equal(expected, config["ReverseProxy:Clusters:notification:Destinations:d1:Address"]);
            foreach (var setting in local.AsEnumerable().Where(pair =>
                pair.Key != "ReverseProxy:Clusters:notification:Destinations:d1:Address"))
                Assert.Equal(setting.Value, config[setting.Key]);
            Assert.Equal("/notification/{**catch-all}", config["ReverseProxy:Routes:notification-route:Match:Path"]);
            var transforms = config.GetSection("ReverseProxy:Routes:notification-route:Transforms").GetChildren().ToArray();
            Assert.Single(transforms);
            Assert.Equal("/{**catch-all}", transforms[0]["PathPattern"]);
            Assert.Single(transforms[0].GetChildren()); // No header removal/replacement transform.
        }
        finally { Environment.SetEnvironmentVariable(variable, null); }
    }

    [Fact]
    public void NotificationRoute_StripsPrefixAndKeepsExistingRoutes()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ToeicLearning.sln"))) root = root.Parent;
        Assert.NotNull(root);
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName,
            "src", "ApiGateways", "ApiGateway", "appsettings.json")));
        var proxy = json.RootElement.GetProperty("ReverseProxy");
        var route = proxy.GetProperty("Routes").GetProperty("notification-route");
        Assert.Equal("notification", route.GetProperty("ClusterId").GetString());
        Assert.Equal("/notification/{**catch-all}", route.GetProperty("Match").GetProperty("Path").GetString());
        Assert.Equal("/{**catch-all}", route.GetProperty("Transforms")[0].GetProperty("PathPattern").GetString());
        Assert.Equal("http://localhost:5010/", proxy.GetProperty("Clusters").GetProperty("notification")
            .GetProperty("Destinations").GetProperty("d1").GetProperty("Address").GetString());
        foreach (var service in new[] { "identity", "catalog", "exam", "progress", "learning", "content" })
        {
            Assert.True(proxy.GetProperty("Routes").TryGetProperty($"{service}-route", out _));
            Assert.True(proxy.GetProperty("Clusters").TryGetProperty(service, out _));
        }
    }
}
