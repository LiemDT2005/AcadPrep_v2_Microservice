using System.Text;
using BuildingBlocks.EventBus;
using Exam.Data;
using Exam.Mappings;
using Exam.Repositories;
using Exam.Repositories.Interfaces;
using Exam.Services;
using Exam.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Database
var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=exam.db";
var isSqlServer = string.Equals(builder.Configuration["UseSqlServer"], "true", StringComparison.OrdinalIgnoreCase) ||
                  connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
                  connectionString.Contains("Data Source=.", StringComparison.OrdinalIgnoreCase) ||
                  connectionString.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
                  connectionString.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase);

builder.Services.AddDbContext<ExamDbContext>(opt =>
{
    if (isSqlServer)
        opt.UseSqlServer(connectionString);
    else
        opt.UseSqlite(connectionString);
});
builder.Services.AddScoped<IExamDbContext>(sp => sp.GetRequiredService<ExamDbContext>());
builder.Services.AddEventBus(builder.Configuration);

// Repositories and application services share the request-scoped DbContext.
builder.Services.AddScoped<IExamRepository, ExamRepository>();
builder.Services.AddScoped<IQuestionRepository, QuestionRepository>();
builder.Services.AddScoped<IExamAttemptRepository, ExamAttemptRepository>();
builder.Services.AddScoped<IPracticeSessionRepository, PracticeSessionRepository>();
builder.Services.AddHttpClient<IExamPaperSource, HttpExamPaperSource>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Content:BaseUrl"] ?? "http://localhost:5006/");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddScoped<IExamQuotaPolicy, PendingBillingQuotaPolicy>();
builder.Services.AddScoped<ISessionStateRepository, SessionStateRepository>();
builder.Services.AddScoped<IExamLifecycleService, ExamLifecycleService>();
builder.Services.AddHostedService<Exam.Messaging.SubmissionOutboxWorker>();
builder.Services.AddHostedService<Exam.Messaging.ExamExpiryWorker>();
builder.Services.AddScoped<IExamAttemptService, ExamAttemptService>();
builder.Services.AddScoped<IExamSessionService, ExamSessionService>();
builder.Services.AddAutoMapper(cfg =>
{
    cfg.LicenseKey = builder.Configuration["AutoMapper:LicenseKey"];
}, typeof(ExamMappingProfile));

// Authentication
var jwt = builder.Configuration.GetSection("Jwt");
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!))
        };
    });

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Exam API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", In = ParameterLocation.Header, Type = SecuritySchemeType.Http, Scheme = "bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
await ExamSeeder.SeedAsync(app.Services);
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (DbUpdateConcurrencyException)
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new { error = "The session changed in another request. Reload or retry." });
    }
});
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Exam" }));
app.Run();
