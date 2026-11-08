using System.Collections.Concurrent;
using System.Xml.Linq;
using Identity.API.Hubs;
using Identity.API.IServices;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;

namespace Identity.Tests;

public class InMemoryXmlRepository : IXmlRepository
{
    private readonly List<XElement> _elements = new();

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        lock (_elements)
        {
            return _elements.Select(e => new XElement(e)).ToList().AsReadOnly();
        }
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        lock (_elements)
        {
            _elements.Add(new XElement(element));
        }
    }
}

public class TestEmailService : IEmailService
{
    public ConcurrentDictionary<string, string> SentOtps { get; } = new();

    public Task SendOtpEmailAsync(string toEmail, string otpCode, string recipientName)
    {
        SentOtps[toEmail.Trim().ToLowerInvariant()] = otpCode;
        return Task.CompletedTask;
    }
}

public class TestHubContext : IHubContext<UserHub>
{
    private readonly TestHubClients _clients = new();

    public IHubClients Clients => _clients;
    public IGroupManager Groups => throw new NotImplementedException();

    public ConcurrentDictionary<string, List<(string Method, object?[] Args)>> SentMessages => _clients.SentMessages;
}

public class TestHubClients : IHubClients, IClientProxy
{
    public ConcurrentDictionary<string, List<(string Method, object?[] Args)>> SentMessages { get; } = new();
    private string _currentUserId = string.Empty;

    public IClientProxy All => this;
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => this;
    public IClientProxy Client(string connectionId) => this;
    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => this;
    public IClientProxy Group(string groupName) => this;
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => this;
    public IClientProxy Groups(IReadOnlyList<string> groupNames) => this;

    public IClientProxy User(string userId)
    {
        _currentUserId = userId;
        return this;
    }

    public IClientProxy Users(IReadOnlyList<string> userIds) => this;

    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
    {
        var list = SentMessages.GetOrAdd(_currentUserId, _ => new List<(string, object?[])>());
        lock (list)
        {
            list.Add((method, args));
        }
        return Task.CompletedTask;
    }
}

public class TestConfiguration
{
    public static IConfiguration Create(string projectRoot)
    {
        var privateKeyPath = Path.Combine(projectRoot, "src", "Services", "Identity", "Identity.API", "keys", "private_key.pem");
        var publicKeyPath = Path.Combine(projectRoot, "src", "Services", "Identity", "Identity.API", "keys", "public_key.pem");

        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Jwt:PrivateKeyPath", privateKeyPath },
            { "Jwt:PublicKeyPath", publicKeyPath },
            { "Jwt:Issuer", "ToeicLearning.Identity" },
            { "Jwt:Audience", "ToeicLearning.Clients" },
            { "Jwt:ExpireHours", "8" },
            { "Jwt:RefreshTokenExpireDays", "7" },
            { "Redis:ConnectionString", "localhost:6379" },
            { "Authentication:Google:ClientId", "mock-google-client-id" },
            { "EmailSettings:SmtpHost", "" }
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }
}
