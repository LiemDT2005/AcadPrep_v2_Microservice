using StackExchange.Redis;
using BuildingBlocks.Common.Authentication;
using BuildingBlocks.Common.Events;
using BuildingBlocks.EventBus;
using BuildingBlocks.EventBus.Abstractions;
using Learning.API.BackgroundJobs;
using Learning.API.Caching;
using Learning.API.Data;
using Learning.API.Entities;
using Learning.API.Formatters;
using Learning.API.Hubs;
using Learning.API.Mappings;
using Learning.API.Messaging;
using Learning.API.Repositories;
using Learning.API.Repositories.Interfaces;
using Learning.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OData;
using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Database Connection (Dynamic, No Hardcoding)
var connectionString = builder.Configuration.GetConnectionString("Default") 
    ?? builder.Configuration.GetConnectionString("Sqlite") 
    ?? "Data Source=learning.db";
var isSqlServer = string.Equals(builder.Configuration["UseSqlServer"], "true", StringComparison.OrdinalIgnoreCase) ||
                  connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
                  connectionString.Contains("Data Source=.", StringComparison.OrdinalIgnoreCase) ||
                  connectionString.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
                  connectionString.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase);

builder.Services.AddDbContext<LearningDbContext>(opt =>
{
    if (isSqlServer)
        opt.UseSqlServer(connectionString);
    else
        opt.UseSqlite(connectionString);
});
builder.Services.AddScoped<ILearningDbContext>(sp => sp.GetRequiredService<LearningDbContext>());

// 2. Redis Distributed Cache
var redisConn = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddStackExchangeRedisCache(opt => opt.Configuration = redisConn);
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => {
    var opts = ConfigurationOptions.Parse(redisConn);
    opts.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(opts);
});
builder.Services.AddSingleton<ILearningCache, RedisLearningCache>();

// 3. SignalR Real-time Hub & Notifier
builder.Services.AddSignalR();
builder.Services.AddScoped<ILearningRealtimeNotifier, SignalRRealtimeNotifier>();

// 4. Repositories
builder.Services.AddScoped<IVocabularyRepository, VocabularyRepository>();
builder.Services.AddScoped<IGamificationRepository, GamificationRepository>();
builder.Services.AddScoped<IExamAttemptRepository, ExamAttemptRepository>();
builder.Services.AddScoped<IUserProfileRepository, UserProfileRepository>();

// 5. Application Services
builder.Services.AddScoped<VocabularyService>();
builder.Services.AddScoped<GamificationService>();
builder.Services.AddScoped<LeaderboardService>();
builder.Services.AddScoped<PerformanceService>();
builder.Services.AddScoped<LearningAdminService>();

// 6. External HTTP Clients
builder.Services.AddHttpClient<IExamStatisticsSource, HttpExamStatisticsSource>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Exam:BaseUrl"] ?? "http://localhost:5003/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

// 7. EventBus & Messaging
builder.Services.AddEventBus(builder.Configuration);
builder.Services.AddScoped<IIntegrationEventHandler<ExamCompletedEvent>, TestSubmittedHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<TestSubmittedEvent>, TestSubmittedHandler>();
builder.Services.AddScoped<TestSubmittedHandler>();

// 8. Background Workers
builder.Services.AddHostedService<StudyStreakResetWorker>();
builder.Services.AddHostedService<LearningEventSubscriptionWorker>();

// 9. AutoMapper
builder.Services.AddAutoMapper(cfg =>
{
    cfg.LicenseKey = builder.Configuration["AutoMapper:LicenseKey"];
}, typeof(LearningMappingProfile));

// 10. JWT Authentication Setup
builder.Services.AddIdentityJwtAuthentication(builder.Configuration, builder.Environment);

// 11. Controllers + CSV Output Formatter + OData
builder.Services.AddControllers(options =>
    {
        options.RespectBrowserAcceptHeader = true;
        options.OutputFormatters.Add(new CsvOutputFormatter());
    })
    .AddOData(opt => opt
        .Select()
        .Filter()
        .OrderBy()
        .Expand()
        .Count()
        .SetMaxTop(100)
        .AddRouteComponents("odata", GetEdmModel()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Learning Service API (AcadPrep TV03)",
        Version = "v1",
        Description = "Microservice phu trach Flashcards (SRS), So tay tu vung, Gamification (Streak/Huy hieu), Bang xep hang, Phan tich hieu suat va Bao cao Admin. Tich hop SignalR, OData, Custom CSV Formatter va Custom Validation."
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        Description = "Nhap JWT token duoc cap tu Identity Service."
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

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.SetIsOriginAllowed(_ => true)
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()));

var app = builder.Build();

// 12. Auto-migrate / EnsureCreated & Seeding
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
    await db.Database.EnsureCreatedAsync();
    await LearningSeeder.SeedAsync(db);
}

// 13. EventBus Subscription
try
{
    var bus = app.Services.GetRequiredService<IEventBus>();
    bus.Subscribe<ExamCompletedEvent, TestSubmittedHandler>();
    bus.Subscribe<TestSubmittedEvent, TestSubmittedHandler>();
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
app.MapHub<LearningHub>("/hubs/learning");

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "LearningService", port = 5005 }));

app.Run();

static IEdmModel GetEdmModel()
{
    var odataBuilder = new ODataConventionModelBuilder();
    odataBuilder.EntitySet<Vocabulary>("Vocabularies");
    odataBuilder.EntitySet<SavedVocabulary>("SavedVocabularies");
    odataBuilder.EntitySet<ExamAttemptRecord>("ExamAttemptRecords").EntityType.HasKey(x => x.AttemptId);
    odataBuilder.EntitySet<Achievement>("Achievements");
    return odataBuilder.GetEdmModel();
}
