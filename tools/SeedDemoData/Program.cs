using Content.Data;
using Exam.Data;
using Identity.API.Data;
using Identity.API.Models;
using Learning.API.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Progress.Infrastructure.Persistence;
using SeedDemoData;

var root = new DirectoryInfo(AppContext.BaseDirectory);
while (root is not null && !File.Exists(Path.Combine(root.FullName, "ToeicLearning.sln"))) root = root.Parent;
if (root is null) throw new InvalidOperationException("Run this tool from the AcadPrep microservice repository.");
var sqliteIndex = Array.IndexOf(args, "--sqlite-dir");
if (sqliteIndex >= 0 && sqliteIndex + 1 >= args.Length)
    throw new ArgumentException("--sqlite-dir requires a directory.");
var sqliteDir = sqliteIndex >= 0 ? Path.GetFullPath(args[sqliteIndex + 1]) : null;
if (sqliteDir is not null) Directory.CreateDirectory(sqliteDir);
var localSql = args.Contains("--local-sql");
if (args.Any(a => a.StartsWith("--") && a is not ("--sqlite-dir" or "--local-sql")))
    throw new ArgumentException("Supported options: --local-sql or --sqlite-dir PATH.");
if (localSql && sqliteDir is not null) throw new ArgumentException("Choose --local-sql or --sqlite-dir, not both.");

string ServicePath(string service) => service is "Exam" or "Content"
    ? Path.Combine(root.FullName, "src", "Services", service)
    : Path.Combine(root.FullName, "src", "Services", service, service + ".API");

IConfiguration Config(string service)
{
    var path = ServicePath(service);
    return new ConfigurationBuilder().SetBasePath(path).AddJsonFile("appsettings.json")
        .AddJsonFile("appsettings.Development.json", optional: true).AddEnvironmentVariables(service + "__").Build();
}
var localBase = Config("Identity").GetConnectionString("Default");
DbContextOptions<T> Options<T>(string service) where T : DbContext
{
    var config = Config(service);
    var connection = config.GetConnectionString(service == "Content" ? "Sqlite" : "Default")
        ?? $"Data Source={service.ToLowerInvariant()}.db";
    if (sqliteDir is not null) connection = $"Data Source={Path.Combine(sqliteDir, service.ToLowerInvariant() + ".db")}";
    else if (localSql && service != "Content")
    {
        if (string.IsNullOrEmpty(localBase)) throw new InvalidOperationException("Identity Default SQL connection is required for --local-sql.");
        var sql = new SqlConnectionStringBuilder(localBase) { InitialCatalog = $"AcadPrep_{service}Db" };
        connection = sql.ConnectionString;
    }
    var builder = new DbContextOptionsBuilder<T>();
    if (connection.Contains("Server=", StringComparison.OrdinalIgnoreCase) || connection.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase))
        builder.UseSqlServer(connection);
    else
    {
        var sqlite = new SqliteConnectionStringBuilder(connection);
        if (!Path.IsPathRooted(sqlite.DataSource))
            sqlite.DataSource = Path.Combine(ServicePath(service), sqlite.DataSource);
        builder.UseSqlite(sqlite.ConnectionString);
    }
    return builder.Options;
}

// This explicit development tool adds demo data only. It never deletes databases or resets existing accounts.
var services = new ServiceCollection();
services.AddLogging(b => b.AddSimpleConsole().SetMinimumLevel(LogLevel.Warning));
services.AddScoped(_ => new IdentityDbContext(Options<IdentityDbContext>("Identity")));
services.AddIdentityCore<AppUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<IdentityDbContext>();
services.AddScoped(_ => new ExamDbContext(Options<ExamDbContext>("Exam")));
services.AddScoped(_ => new LearningDbContext(Options<LearningDbContext>("Learning")));
services.AddScoped(_ => new ProgressDbContext(Options<ProgressDbContext>("Progress")));
services.AddScoped(_ => new ContentDbContext(Options<ContentDbContext>("Content")));
await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var sp = scope.ServiceProvider;
var identity = sp.GetRequiredService<IdentityDbContext>();
// SQL Server uses the application's migrations. SQLite test mode uses the same EF model.
if (identity.Database.IsSqlServer()) await identity.Database.MigrateAsync();
else await identity.Database.EnsureCreatedAsync();
await IdentityDbInitializer.SeedAsync(sp);
var users = await DemoAccounts.SeedAsync(sp);
await ExamSeeder.SeedAsync(provider);
var learning = sp.GetRequiredService<LearningDbContext>();
await learning.Database.EnsureCreatedAsync();
await LearningSeeder.SeedAsync(learning, includeSampleUsers: false);
var content = sp.GetRequiredService<ContentDbContext>();
await ContentSeeder.SeedAsync(content, sp.GetRequiredService<ILoggerFactory>().CreateLogger("DemoSeed"));
var progress = sp.GetRequiredService<ProgressDbContext>();
await progress.Database.EnsureCreatedAsync();
await DemoContent.SeedAsync(content, sp.GetRequiredService<ExamDbContext>());
await DemoLearning.SeedAsync(learning,
    sp.GetRequiredService<ExamDbContext>(), progress, users);
Console.WriteLine("Demo data ready (existing records preserved). Run again safely to add missing demo records.");
Console.WriteLine($"Identity: {await identity.Users.CountAsync()} users");
Console.WriteLine($"Exam: {await sp.GetRequiredService<ExamDbContext>().Exams.CountAsync()} exams, {await sp.GetRequiredService<ExamDbContext>().ExamAttempts.CountAsync()} attempts");
Console.WriteLine($"Content: {await content.Exams.CountAsync()} exams, {await content.Questions.CountAsync()} questions");
Console.WriteLine($"Learning: {await learning.Vocabularies.CountAsync()} words, {await learning.SavedVocabularies.CountAsync()} saved words, {await learning.ExamAttemptRecords.CountAsync()} score records");
Console.WriteLine($"Progress: {await progress.LearningRecords.CountAsync()} records");
Console.WriteLine("Demo password: Demo@123456 (new demo accounts only; see tools/SeedDemoData/README.md).");
