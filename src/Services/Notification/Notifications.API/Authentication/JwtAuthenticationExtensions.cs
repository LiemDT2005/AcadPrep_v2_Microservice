using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Notifications.Application.Interfaces;

namespace Notifications.API.Authentication;

public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddNotificationAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<NotificationJwtOptions>()
            .Bind(configuration.GetSection(NotificationJwtOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer) &&
                !string.IsNullOrWhiteSpace(options.Audience) && !string.IsNullOrWhiteSpace(options.KeyId),
                "JWT issuer, audience and key identifier are required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.PublicKeyPath) ^
                !string.IsNullOrWhiteSpace(options.PublicKey), "Configure exactly one RSA public key source.")
            .ValidateOnStart();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<NotificationJwtOptions>, IWebHostEnvironment>((options, settings, environment) =>
            {
                var jwt = settings.Value;
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    RequireExpirationTime = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = LoadPublicKey(jwt, environment.ContentRootPath),
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    RoleClaimType = ClaimTypes.Role,
                    NameClaimType = "name",
                    ClockSkew = TimeSpan.Zero
                };
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

        // Validate key configuration at startup, without touching external services.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).ValidateOnStart();
        services.AddAuthorization();
        return services;
    }

    private static RsaSecurityKey LoadPublicKey(NotificationJwtOptions options, string contentRoot)
    {
        try
        {
            var pem = string.IsNullOrWhiteSpace(options.PublicKeyPath)
                ? options.PublicKey!
                : File.ReadAllText(Path.GetFullPath(options.PublicKeyPath, contentRoot));
            if (!PemEncoding.TryFind(pem, out var fields) ||
                (pem[fields.Label] != "PUBLIC KEY" && pem[fields.Label] != "RSA PUBLIC KEY") ||
                !string.IsNullOrWhiteSpace(pem[..fields.Location.Start.GetOffset(pem.Length)]) ||
                !string.IsNullOrWhiteSpace(pem[fields.Location.End.GetOffset(pem.Length)..]))
                throw new CryptographicException();

            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            if (rsa.KeySize < 2048) throw new CryptographicException();
            return new RsaSecurityKey(rsa.ExportParameters(false)) { KeyId = options.KeyId };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            CryptographicException or ArgumentException or NotSupportedException)
        {
            // Do not include PEM contents, filesystem paths or the inner exception.
            throw new OptionsValidationException(NotificationJwtOptions.SectionName, typeof(NotificationJwtOptions),
                ["A readable RSA public key of at least 2048 bits is required; private keys are not accepted."]);
        }
    }
}
