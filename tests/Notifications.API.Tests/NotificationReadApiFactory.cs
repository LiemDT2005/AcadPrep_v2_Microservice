using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Notifications.Application.Interfaces;
using Notifications.Domain.Entities;
using Notifications.Domain.Enums;
using Notifications.Infrastructure.Persistence;
using Xunit;

namespace Notifications.API.Tests;

public class NotificationReadApiFactory : NotificationsApiFactory, IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    public CommandRecorder Commands { get; } = new();
    public static Guid LearnerId => Id(1001);
    public static Guid OtherUserId => Id(1002);
    public static Guid AdminId => Id(1003);
    public static Guid EmptyUserId => Id(1004);
    public static DateTime Epoch => new(2026, 10, 18, 10, 0, 0, DateTimeKind.Utc);
    public static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.AddScoped(_ => new SqliteReadDbContext(new DbContextOptionsBuilder<NotificationsDbContext>()
                .UseSqlite(connection).AddInterceptors(Commands).Options));
            services.RemoveAll<INotificationsDbContext>();
            services.AddScoped<INotificationsDbContext>(provider => provider.GetRequiredService<SqliteReadDbContext>());
        });
    }

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SqliteReadDbContext>();
        // Only this isolated in-memory SQLite database is created. Production mappings/migrations are untouched.
        await db.Database.EnsureCreatedAsync();
        var notifications = new[]
        {
            Item(1, NotificationAudience.User, LearnerId), Item(2, NotificationAudience.User, OtherUserId),
            Item(3, NotificationAudience.All), Item(4, NotificationAudience.Learner),
            Item(5, NotificationAudience.Moderator), Item(6, NotificationAudience.All),
            Item(7, NotificationAudience.All), Item(8, NotificationAudience.All),
            Item(9, NotificationAudience.All), Item(10, NotificationAudience.User, LearnerId),
            Item(11, NotificationAudience.All), Item(12, NotificationAudience.User, AdminId)
        };
        db.Notifications.AddRange(notifications);
        db.Notifications.AddRange(Enumerable.Range(20, 25).Select(number =>
            Item(number, NotificationAudience.User, LearnerId, Epoch.AddDays(1))));
        db.UserNotificationStates.AddRange(
            State(2, LearnerId), State(6, LearnerId, read: true), State(7, LearnerId),
            State(8, LearnerId, deleted: true), State(9, OtherUserId, read: true, deleted: true),
            State(10, LearnerId, read: true), State(11, LearnerId, read: true, deleted: true));
        db.UserNotificationStates.AddRange(notifications.Where(item => item.Audience == NotificationAudience.All)
            .Select(item => new UserNotificationState
            {
                NotificationId = item.Id, UserId = EmptyUserId, IsDeleted = true, DeletedAt = Epoch
            }));
        await db.SaveChangesAsync();
        Commands.Reset();
    }

    Task IAsyncLifetime.DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) connection.Dispose();
    }

    private static Notification Item(int number, NotificationAudience audience, Guid? target = null, DateTime? at = null) => new()
    {
        Id = Id(number), Title = $"Notice {number}", Content = $"Content {number}", Audience = audience,
        TargetUserId = target, CreatedAt = at ?? Epoch.AddMinutes(-number),
        DataJson = "{\"internal\":true}", CreatedBy = AdminId, SourceEventId = Id(2000 + number)
    };

    private static UserNotificationState State(int number, Guid userId, bool read = false, bool deleted = false) => new()
    {
        NotificationId = Id(number), UserId = userId, IsRead = read, ReadAt = read ? Epoch : null,
        IsDeleted = deleted, DeletedAt = deleted ? Epoch : null
    };
}

// Provider adaptations exist only here. They are not evidence that SQL Server DDL was executed.
public class SqliteReadDbContext(DbContextOptions<NotificationsDbContext> options) : NotificationsDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnType(null);
                property.SetDefaultValueSql(null);
            }
        }
        modelBuilder.Entity<Notification>().ToTable("Notifications", table =>
        {
            table.HasCheckConstraint("CK_Notifications_Type", "[Type] IN ('System', 'ExamResult', 'Subscription', 'Reminder')");
            table.HasCheckConstraint("CK_Notifications_Audience_TargetUserId",
                "([Audience] = 'User' AND [TargetUserId] IS NOT NULL) OR " +
                "([Audience] IN ('All', 'Learner', 'Moderator') AND [TargetUserId] IS NULL)");
        });
    }
}

public class CommandRecorder : DbCommandInterceptor
{
    private readonly ConcurrentQueue<string> commands = new();
    public string[] Snapshot => commands.ToArray();
    public void Reset() => commands.Clear();

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        commands.Enqueue(command.CommandText);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        commands.Enqueue(command.CommandText);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        commands.Enqueue(command.CommandText);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }
}
