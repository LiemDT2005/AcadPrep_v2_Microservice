using System.Text;
using Learning.Application;
using Learning.Infrastructure;
using Learning.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Layer Dependency Injections
builder.Services.AddHttpClient<Learning.Application.Interfaces.IExamStatisticsSource, Learning.Infrastructure.Messaging.HttpExamStatisticsSource>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Exam:BaseUrl"] ?? "http://localhost:5003/");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// JWT Authentication Setup
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
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Learning Service API (AcadPrep TV03)",
        Version = "v1",
        Description = "Microservice phụ trách Flashcards (SRS), Sổ tay từ vựng, Gamification (Streak/Huy hiệu), Bảng xếp hạng, Phân tích hiệu suất và Báo cáo Admin."
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        Description = "Nhập JWT token được cấp từ Identity Service."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Auto-migrate & Seed Database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
    await db.Database.EnsureCreatedAsync();
    await LearningSeeder.SeedAsync(db);
}

// Subscribe EventBus events (ExamCompletedEvent / TestSubmittedEvent from TV02)
try
{
    app.Services.SubscribeEvents();
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Could not subscribe to RabbitMQ on startup. Service will continue.");
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Learning API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "LearningService", port = 5005 }));

app.Run();
