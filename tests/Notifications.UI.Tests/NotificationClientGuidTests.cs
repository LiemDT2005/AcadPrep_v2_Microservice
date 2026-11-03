using Microsoft.AspNetCore.Http;
using Storefront.Web.Models;
using Storefront.Web.Services;
using Xunit;

namespace Notifications.UI.Tests;

public class NotificationClientGuidTests
{
    [Theory]
    [InlineData("invalid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task InvalidTargetReturnsFieldErrorWithoutSendingRequest(string target)
    {
        using var backend = new NotificationBackend();
        using var http = new HttpClient(backend) { BaseAddress = new Uri("http://gateway.test") };
        var client = new ToeicApiClient(http, new HttpContextAccessor());

        var result = await client.CreateNotificationAsync(new NotificationInput
        {
            Title = "Title", Content = "Content", Audience = "User", TargetUserId = target
        }, CancellationToken.None);

        Assert.Equal(400, result.Status);
        Assert.Contains("targetUserId", result.Errors!.Keys);
        Assert.Empty(backend.Calls);
    }
}
