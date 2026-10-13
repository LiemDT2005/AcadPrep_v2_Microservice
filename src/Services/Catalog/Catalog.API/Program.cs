using System.Security.Cryptography;
using Catalog.Application;
using Catalog.Infrastructure;
using Catalog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var jwt = builder.Configuration.GetSection("Jwt");
var keyPath = jwt["PublicKeyPath"];
string publicKeyPem;
if (!string.IsNullOrEmpty(keyPath))
{
    var fullPath = Path.IsPathRooted(keyPath) ? keyPath : Path.Combine(builder.Environment.ContentRootPath, keyPath);
    publicKeyPem = File.Exists(fullPath) ? File.ReadAllText(fullPath) : (jwt["PublicKey"] ?? string.Empty);
}
else
{
    publicKeyPem = jwt["PublicKey"] ?? string.Empty;
}

var rsaVerify = RSA.Create();
rsaVerify.ImportFromPem(publicKeyPem);
var rsaSecurityKey = new RsaSecurityKey(rsaVerify) { KeyId = "acadprep-rsa-key-1" };

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = rsaSecurityKey
        };
    });

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new OpenApiInfo { Title = "Catalog API", Version = "v1" }));
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
await CatalogSeeder.SeedAsync(app.Services);

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Catalog" }));
app.Run();
