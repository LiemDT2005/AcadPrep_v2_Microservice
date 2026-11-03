using BuildingBlocks.Common.Authentication;
using Content.Data;
using Content.Mappings;
using Content.Repositories;
using Content.Repositories.Interfaces;
using Content.Services;
using Content.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Database & Infrastructure
var connStr = builder.Configuration.GetConnectionString("Sqlite") ?? "Data Source=content.db";
builder.Services.AddDbContext<ContentDbContext>(options =>
    options.UseSqlite(connStr));

builder.Services.AddScoped<IContentDbContext>(sp => sp.GetRequiredService<ContentDbContext>());

// 2. HttpClient & Service Injections
builder.Services.AddHttpClient<IExamAttemptChecker, HttpExamAttemptChecker>(client =>
{
    client.Timeout = TimeSpan.FromMilliseconds(500);
});
builder.Services.AddScoped<IExamRepository, ExamRepository>();
builder.Services.AddScoped<IExamSeriesRepository, ExamSeriesRepository>();
builder.Services.AddScoped<IContentService, ContentService>();
builder.Services.AddScoped<IExamService, ExamService>();
builder.Services.AddAutoMapper(cfg =>
{
    cfg.LicenseKey = builder.Configuration["AutoMapper:LicenseKey"];
}, typeof(ContentMappingProfile));

// 3. JWT Authentication Setup
builder.Services.AddIdentityJwtAuthentication(builder.Configuration, builder.Environment);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Content Service API (AcadPrep Exams, Questions & Passages CMS)",
        Version = "v1",
        Description = "Microservice phụ trách danh sách/chi tiết đề Learner (UC-3.1–3.4), CMS Exams (UC-16.1–16.7), Question Bank (UC-17.1–17.7) và Reading Passages (UC-18.1–18.5)."
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

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

// 4. Auto Migration & Seeding
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await ContentSeeder.SeedAsync(db, logger);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Content" }));

app.Run();
