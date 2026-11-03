using System.Net;
using System.Net.Http.Json;
using Content.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Content.Tests;

public class HttpExamAttemptCheckerTests
{
    [Fact]
    public async Task SuccessfulEndpoint_ReturnsRemoteCount()
    {
        using var client = new HttpClient(new StubHandler((request, ct) =>
        {
            Assert.Equal("http://exam-service/api/exams/1/attempts/count", request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(42) });
        }));
        Assert.Equal(42, await CreateChecker(client).GetAttemptCountAsync(1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrUnreachableEndpoint_PreservesDemoFallback(bool throws)
    {
        using var client = new HttpClient(new StubHandler((request, ct) => throws
            ? Task.FromException<HttpResponseMessage>(new HttpRequestException("Offline"))
            : Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))));
        var checker = CreateChecker(client);
        Assert.Equal(15, await checker.GetAttemptCountAsync(1));
        Assert.Equal(0, await checker.GetAttemptCountAsync(2));
        Assert.Equal(8, await checker.GetAttemptCountAsync(3));
        Assert.Equal(0, await checker.GetAttemptCountAsync(999));
    }

    private static HttpExamAttemptChecker CreateChecker(HttpClient client) => new(client,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Services:ExamApi"] = "http://exam-service"
        }).Build(), NullLogger<HttpExamAttemptChecker>.Instance);

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
