using System.Security.Claims;
using System.Security.Cryptography;
using BuildingBlocks.Common.Authentication;
using BuildingBlocks.EventBus;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Notifications.API.Data;
using Notifications.API.Mappings;
using Notifications.API.Messaging;
using Notifications.API.Repositories;
using Notifications.API.Repositories.Interfaces;
using Notifications.API.Services;
using Notifications.API.Services.Interfaces;

namespace Notifications.API;

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

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAutoMapper(options =>
        {
            options.AddProfile<NotificationMappingProfile>();
            options.LicenseKey = Environment.GetEnvironmentVariable("AUTOMAPPER_LICENSE_KEY");
        });
        services.AddScoped<INotificationQueryService, NotificationQueryService>();
        services.AddScoped<INotificationCommandService, NotificationCommandService>();
        services.AddScoped<INotificationAdminService, NotificationAdminService>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // ConnectionStrings:Default comes from appsettings (F5) or ConnectionStrings__Default (Compose); no auto-migration.
        services.AddDbContext<NotificationsDbContext>(options => options.UseSqlServer(
            configuration.GetConnectionString("Default") ??
            throw new InvalidOperationException("ConnectionStrings:Default is required.")));
        services.AddScoped<INotificationsDbContext>(provider => provider.GetRequiredService<NotificationsDbContext>());
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationStateRepository, NotificationStateRepository>();
        services.AddScoped<IEmailDeliveryRepository, EmailDeliveryRepository>();
        // UC-23.3: Billing reminder events (RabbitMQ) -> in-app notification + SMTP email.
        services.AddEventBus(configuration);
        services.AddOptions<SmtpOptions>().Bind(configuration.GetSection(SmtpOptions.SectionName));
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<ISubscriptionReminderService, SubscriptionReminderService>();
        services.AddScoped<SubscriptionExpiringHandler>();
        services.AddScoped<SubscriptionExpiredHandler>();
        services.AddScoped<SubscriptionActivatedHandler>();
        // Enabled is read when the worker starts (not at registration), so host/test configuration applies.
        services.AddOptions<BackgroundJobOptions>().Bind(configuration.GetSection(BackgroundJobOptions.SectionName));
        services.AddHostedService<ReminderSubscriberWorker>();
        services.AddHttpContextAccessor();
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
        return services;
    }
}
