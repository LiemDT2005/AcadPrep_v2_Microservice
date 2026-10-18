using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Notifications.Application.Interfaces;
using Notifications.Domain.Entities;
using Notifications.Domain.Enums;
using Notifications.Infrastructure.Persistence;
using Xunit;

namespace Notifications.API.Tests;

// Explicit opt-in prevents the ordinary offline suite from opening any SQL Server connection.
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("ACADPREP_NOTIFICATION_SQL_TESTS") != "1")
            Skip = "Opt-in SQL Server test database is required.";
    }
}

[CollectionDefinition("NotificationSqlServer", DisableParallelization = true)]
public class SqlServerNotificationCollection;

public class SqlServerNotificationFactory : NotificationsApiFactory
{
    public const string DatabaseName = "AcadPrep_Notification_N8b_Test";
    private readonly HashSet<Guid> ownedIds = [];
    public SqlServerRaceControl Race { get; } = new();
    public Guid LearnerId { get; } = Guid.NewGuid();
    public Guid OtherUserId { get; } = Guid.NewGuid();

    private static string SafeConnection
    {
        get
        {
            if (Environment.GetEnvironmentVariable("ACADPREP_NOTIFICATION_SQL_TESTS") != "1")
                throw new InvalidOperationException("SQL Server tests require explicit opt-in.");
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = "tcp:localhost,1433", InitialCatalog = DatabaseName,
                IntegratedSecurity = true, Encrypt = true, TrustServerCertificate = true,
                ApplicationName = "AcadPrep_Notification_N8b2_Tests", ConnectTimeout = 5
            };
            if (builder.InitialCatalog != DatabaseName || builder.DataSource != "tcp:localhost,1433")
                throw new InvalidOperationException("Unexpected test database target.");
            return builder.ConnectionString;
        }
    }

    public NotificationsDbContext OpenContext() => new(new DbContextOptionsBuilder<NotificationsDbContext>()
        .UseSqlServer(SafeConnection).Options);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<NotificationsDbContext>();
            services.RemoveAll<DbContextOptions<NotificationsDbContext>>();
            services.AddScoped(_ => OpenContext());
            services.RemoveAll<INotificationsDbContext>();
            services.AddScoped<INotificationsDbContext>(p => p.GetRequiredService<NotificationsDbContext>());
            services.RemoveAll<INotificationReadStateStore>();
            services.AddScoped<INotificationReadStateStore>(p => new ObservedSqlServerStateStore(
                p.GetRequiredService<NotificationsDbContext>(), Race));
        });
    }

    public async Task VerifyTargetAsync()
    {
        await using var db = OpenContext();
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT DB_NAME(), CONVERT(nvarchar(128), SERVERPROPERTY('ServerName'))";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(DatabaseName, reader.GetString(0));
        Assert.Equal("LAPTOP-LTQGDDVG", reader.GetString(1));
        await reader.DisposeAsync();
        Assert.Contains("20261018161833_InitialNotifications", await db.Database.GetAppliedMigrationsAsync());
    }

    public Notification NewNotification(NotificationAudience audience = NotificationAudience.All, Guid? target = null)
    {
        var item = new Notification
        {
            Id = Guid.NewGuid(), Title = "SQL integration test", Content = "Owned test data",
            Audience = audience, TargetUserId = target, CreatedAt = DateTime.UtcNow
        };
        ownedIds.Add(item.Id);
        return item;
    }

    public async Task SaveAsync(params Notification[] items)
    {
        await using var db = OpenContext();
        db.Notifications.AddRange(items);
        await db.SaveChangesAsync();
    }

    public HttpClient AuthenticatedClient(Guid? userId = null, params string[] roles)
    {
        var client = CreateClient();
        var claims = new List<System.Security.Claims.Claim> { new("sub", (userId ?? LearnerId).ToString()) };
        claims.AddRange((roles.Length == 0 ? ["Learner"] : roles).Select(role =>
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role)));
        client.DefaultRequestHeaders.Authorization = new("Bearer", CreateToken(claims: claims));
        return client;
    }

    public async Task<UserNotificationState?> StateAsync(Guid id, Guid? user = null)
    {
        var userId = user ?? LearnerId;
        await using var db = OpenContext();
        return await db.UserNotificationStates.AsNoTracking()
            .SingleOrDefaultAsync(s => s.NotificationId == id && s.UserId == userId);
    }

    public async Task CleanupOwnedAsync()
    {
        // No reset, TRUNCATE, DROP, or broad DELETE. Only GUIDs allocated by this factory are eligible.
        if (ownedIds.Count == 0) return;
        await using var db = OpenContext();
        var ids = ownedIds.ToArray();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.UserNotificationStates.Where(s => ids.Contains(s.NotificationId)).ExecuteDeleteAsync();
        await db.Notifications.Where(n => ids.Contains(n.Id)).ExecuteDeleteAsync();
        await transaction.CommitAsync();
        Assert.False(await db.Notifications.AnyAsync(n => ids.Contains(n.Id)));
    }
}

public class SqlServerRaceControl
{
    public Func<UserNotificationState, CancellationToken, Task>? BeforeInsert { get; set; }
    public Action<UserNotificationState>? AfterInsert { get; set; }
    public int DuplicateConflicts;
    public int LastDuplicateNumber;
}

public class ObservedSqlServerStateStore(NotificationsDbContext db, SqlServerRaceControl race)
    : NotificationReadStateStore(db)
{
    protected override async Task InsertStateAsync(UserNotificationState state, CancellationToken ct)
    {
        if (race.BeforeInsert is not null) await race.BeforeInsert(state, ct);
        await base.InsertStateAsync(state, ct);
        race.AfterInsert?.Invoke(state);
    }

    protected override bool IsDuplicateStateConflict(DbUpdateException exception, UserNotificationState inserted)
    {
        // Use the unmodified production SQL Server classifier, not a test replacement.
        var result = base.IsDuplicateStateConflict(exception, inserted);
        if (result)
        {
            Interlocked.Increment(ref race.DuplicateConflicts);
            race.LastDuplicateNumber = ((SqlException)exception.InnerException!).Number;
        }
        return result;
    }
}
