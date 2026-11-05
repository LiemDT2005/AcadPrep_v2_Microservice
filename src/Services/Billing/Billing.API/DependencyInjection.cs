using System.Security.Claims;
using System.Security.Cryptography;
using BuildingBlocks.Common.Authentication;
using BuildingBlocks.EventBus;
using Billing.API.Data;
using Billing.API.Messaging;
using Billing.API.Repositories;
using Billing.API.Repositories.Interfaces;
using Billing.API.Services;
using Billing.API.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Billing.API;

public static class DependencyInjection
{
    // Identity RS256 tokens through the shared BuildingBlocks helper (public key only, same as Exam), plus stricter
    // claim handling for this service: UserId comes only from exactly one Guid "sub" (see CurrentUser).
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        // Runs before the shared helper loads the key: only a public key may be configured in this service (never
        // Identity's private key). The startup error never contains the PEM or the file path.
        services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, _ =>
            EnsurePublicKeyOnly(configuration, environment.ContentRootPath));
        services.AddIdentityJwtAuthentication(configuration, environment);
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.MapInboundClaims = false;
            options.IncludeErrorDetails = false;
            var validation = options.TokenValidationParameters;
            validation.RequireSignedTokens = true;
            validation.RequireExpirationTime = true;
            validation.RoleClaimType = ClaimTypes.Role;
            validation.NameClaimType = "name";
            validation.ClockSkew = TimeSpan.Zero;
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    var subjects = context.Principal?.FindAll("sub").ToArray() ?? [];
                    if (subjects.Length != 1 || !Guid.TryParse(subjects[0].Value, out var userId) || userId == Guid.Empty)
                        context.Fail("A valid user identifier is required.");
                    return Task.CompletedTask;
                }
            };
        });
        // Fail at startup, not on the first request, when the issuer, audience or public key is missing or weak.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Validate(options => !string.IsNullOrWhiteSpace(options.TokenValidationParameters.ValidIssuer) &&
                !string.IsNullOrWhiteSpace(options.TokenValidationParameters.ValidAudience) &&
                options.TokenValidationParameters.IssuerSigningKey is RsaSecurityKey { Parameters.Modulus.Length: >= 256 },
                "JWT issuer, audience and an RSA public key of at least 2048 bits are required.")
            .ValidateOnStart();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddAuthorization();
        return services;
    }

    private static void EnsurePublicKeyOnly(IConfiguration configuration, string contentRoot)
    {
        bool valid;
        try
        {
            var path = configuration["Jwt:PublicKeyPath"];
            var pem = string.IsNullOrWhiteSpace(path)
                ? configuration["Jwt:PublicKey"] ?? string.Empty
                : File.ReadAllText(Path.GetFullPath(path, contentRoot));
            valid = PemEncoding.TryFind(pem, out var fields) &&
                pem[fields.Label] is "PUBLIC KEY" or "RSA PUBLIC KEY" &&
                string.IsNullOrWhiteSpace(pem[..fields.Location.Start.GetOffset(pem.Length)]) &&
                string.IsNullOrWhiteSpace(pem[fields.Location.End.GetOffset(pem.Length)..]);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException)
        {
            valid = false;
        }
        if (!valid)
            throw new OptionsValidationException(JwtBearerDefaults.AuthenticationScheme, typeof(JwtBearerOptions),
                ["Jwt:PublicKeyPath or Jwt:PublicKey must be one readable RSA public key; private keys are not accepted."]);
    }

    public static IServiceCollection AddBilling(this IServiceCollection services, IConfiguration configuration)
    {
        // ConnectionStrings:Default comes from appsettings (F5) or ConnectionStrings__Default (Compose); no auto-migration.
        services.AddDbContext<BillingDbContext>(options => options.UseSqlServer(
            configuration.GetConnectionString("Default") ??
            throw new InvalidOperationException("ConnectionStrings:Default is required.")));
        services.AddScoped<IPlanRepository, PlanRepository>();
        services.AddScoped<IPlanService, PlanService>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<ISubscriptionGrantService, SubscriptionGrantService>();
        services.AddScoped<IUsageService, UsageService>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderQueryService, OrderQueryService>();
        services.AddScoped<IRevenueRepository, RevenueRepository>();
        services.AddScoped<IRevenueService, RevenueService>();
        // UC-23.3 reminders/expiry -> transactional outbox -> RabbitMQ (BuildingBlocks.EventBus) -> Notification.
        services.AddEventBus(configuration);
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<ISubscriptionLifecycleRepository, SubscriptionLifecycleRepository>();
        services.AddScoped<ISubscriptionLifecycleService, SubscriptionLifecycleService>();
        services.AddScoped<IOutboxPublisher, OutboxPublisher>();
        // Enabled is read when each worker starts (not at registration), so host/test configuration applies.
        services.AddOptions<BackgroundJobOptions>().Bind(configuration.GetSection(BackgroundJobOptions.SectionName));
        services.AddHostedService<SubscriptionLifecycleWorker>();
        services.AddHostedService<OutboxPublisherWorker>();
        services.AddHostedService<OrderExpiryWorker>();
        // UC-21.2 VNPay: secrets only from user-secrets/environment.
        services.AddOptions<VnpayOptions>().Bind(configuration.GetSection(VnpayOptions.SectionName));
        services.AddOptions<FrontendOptions>().Bind(configuration.GetSection(FrontendOptions.SectionName));
        services.AddSingleton<VnpayClient>();
        services.AddScoped<ICheckoutService, CheckoutService>();
        services.AddScoped<IPaymentConfirmationService, PaymentConfirmationService>();
        services.AddScoped<IOrderExpiryService, OrderExpiryService>();
        // Redis is optional locally: empty Redis:ConnectionString disables the cache (database only).
        // Decided at resolve time so host/test configuration applies; RedisCache connects lazily on first use.
        services.AddStackExchangeRedisCache(_ => { });
        services.AddOptions<Microsoft.Extensions.Caching.StackExchangeRedis.RedisCacheOptions>()
            .Configure<IConfiguration>((options, config) =>
            {
                var redis = config["Redis:ConnectionString"];
                if (string.IsNullOrWhiteSpace(redis)) return;
                var redisOptions = StackExchange.Redis.ConfigurationOptions.Parse(redis);
                redisOptions.AbortOnConnectFail = false;
                redisOptions.ConnectTimeout = 2000;
                redisOptions.SyncTimeout = 2000;
                options.ConfigurationOptions = redisOptions;
            });
        services.AddSingleton<IRevenueCache>(provider =>
            string.IsNullOrWhiteSpace(provider.GetRequiredService<IConfiguration>()["Redis:ConnectionString"])
                ? new NoRevenueCache()
                : ActivatorUtilities.CreateInstance<RevenueCache>(provider));
        services.AddHttpContextAccessor();
        services.AddOptions<AiUsageOptions>().Bind(configuration.GetSection(AiUsageOptions.SectionName))
            .Validate(options => string.IsNullOrWhiteSpace(options.BaseUrl) ||
                Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https",
                "AiService:BaseUrl must be empty or an absolute http(s) URL.")
            .ValidateOnStart();
        services.AddOptions<IdentityOptions>().Bind(configuration.GetSection(IdentityOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) &&
                uri.Scheme is "http" or "https", "Identity:BaseUrl must be an absolute http(s) URL.")
            .ValidateOnStart();
        services.AddHttpClient<IIdentityUserClient, IdentityUserClient>((provider, client) =>
        {
            var url = provider.GetRequiredService<IOptions<IdentityOptions>>().Value.BaseUrl!;
            client.BaseAddress = new Uri(url.EndsWith('/') ? url : url + "/");
            client.Timeout = IdentityUserClient.Timeout;
        });
        services.AddHttpClient<IAiUsageClient, AiUsageClient>((provider, client) =>
        {
            var url = provider.GetRequiredService<IOptions<AiUsageOptions>>().Value.BaseUrl;
            if (!string.IsNullOrWhiteSpace(url)) client.BaseAddress = new Uri(url.EndsWith('/') ? url : url + "/");
            client.Timeout = AiUsageClient.Timeout;
        });
        return services;
    }
}
