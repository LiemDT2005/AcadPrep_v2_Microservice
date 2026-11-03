using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Common.Authentication;

public static class IdentityAuthenticationExtensions
{
    public static IServiceCollection AddIdentityJwtAuthentication(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => options.TokenValidationParameters =
                CreateValidationParameters(configuration, environment.ContentRootPath));
        return services;
    }

    public static TokenValidationParameters CreateValidationParameters(
        IConfiguration configuration, string contentRoot)
    {
        var jwt = configuration.GetSection("Jwt");
        var pem = jwt["PublicKey"];
        if (!string.IsNullOrWhiteSpace(jwt["PublicKeyPath"]))
        {
            var path = Path.GetFullPath(jwt["PublicKeyPath"]!, contentRoot);
            pem = File.ReadAllText(path);
        }
        if (string.IsNullOrWhiteSpace(pem))
            throw new InvalidOperationException("Configure Jwt:PublicKeyPath or Jwt:PublicKey with Identity's RSA public key.");

        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            IssuerSigningKey = new RsaSecurityKey(rsa.ExportParameters(false))
            {
                KeyId = "acadprep-rsa-key-1"
            }
        };
    }
}
