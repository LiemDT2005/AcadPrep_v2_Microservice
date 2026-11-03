using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Notifications.Application.Interfaces;
using Notifications.Domain.Entities;
using Notifications.Domain.Enums;
using Notifications.Infrastructure.Persistence;

namespace Notifications.API.Tests;

public class NotificationMarkReadApiFactory : NotificationsApiFactory
{
    private readonly string connectionString = $"Data Source=notifications-mark-read-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Default Timeout=5;Foreign Keys=True";
    private SqliteConnection? keeper;
    public ReadRaceControl Race { get; } = new();
    public AdvancingClock Clock { get; } = new();
    public static Guid LearnerId => NotificationReadApiFactory.LearnerId;
    public static Guid OtherUserId => NotificationReadApiFactory.OtherUserId;
    public static Guid Id(int number) => NotificationReadApiFactory.Id(number);
    public static DateTime Epoch => NotificationReadApiFactory.Epoch;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.AddScoped(_ => new SqliteReadDbContext(new DbContextOptionsBuilder<NotificationsDbContext>()
                .UseSqlite(connectionString).Options));
            services.RemoveAll<INotificationsDbContext>();
            services.AddScoped<INotificationsDbContext>(provider => provider.GetRequiredService<SqliteReadDbContext>());
            services.RemoveAll<INotificationReadStateStore>();
            services.AddScoped<INotificationReadStateStore>(provider =>
                new SqliteMarkReadStore(provider.GetRequiredService<SqliteReadDbContext>(), Race));
            services.RemoveAll<INotificationWriter>();
            services.AddScoped<INotificationWriter>(provider =>
                new NotificationWriter(provider.GetRequiredService<SqliteReadDbContext>()));
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    public async Task InitializeAsync()
    {
        keeper = new SqliteConnection(connectionString);
        await keeper.OpenAsync();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SqliteReadDbContext>();
        // Only the named, isolated in-memory SQLite database owned by this factory is initialized.
        await db.Database.EnsureCreatedAsync();
        db.Notifications.AddRange(
            Item(1, NotificationAudience.User, LearnerId), Item(2, NotificationAudience.User, OtherUserId),
            Item(3, NotificationAudience.All), Item(4, NotificationAudience.Learner),
            Item(5, NotificationAudience.Moderator), Item(6, NotificationAudience.All),
            Item(7, NotificationAudience.All), Item(8, NotificationAudience.All),
            Item(9, NotificationAudience.All), Item(10, NotificationAudience.All));
        db.UserNotificationStates.AddRange(
            new UserNotificationState { NotificationId = Id(6), UserId = LearnerId, IsRead = true, ReadAt = Epoch },
            new UserNotificationState { NotificationId = Id(7), UserId = LearnerId, IsRead = false },
            new UserNotificationState { NotificationId = Id(8), UserId = LearnerId, IsDeleted = true, DeletedAt = Epoch },
            new UserNotificationState { NotificationId = Id(9), UserId = OtherUserId, IsRead = true, ReadAt = Epoch },
            new UserNotificationState { NotificationId = Id(10), UserId = LearnerId, IsRead = false, ReadAt = Epoch });
        await db.SaveChangesAsync();
    }

    public async Task<T> WithDbAsync<T>(Func<SqliteReadDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<SqliteReadDbContext>());
    }

    public async Task<UserNotificationState?> StateAsync(int number, Guid? user = null)
    {
        var userId = user ?? LearnerId;
        var notificationId = Id(number);
        return await WithDbAsync(db => db.UserNotificationStates.AsNoTracking()
            .SingleOrDefaultAsync(state => state.NotificationId == notificationId && state.UserId == userId));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) keeper?.Dispose();
    }

    private static Notification Item(int number, NotificationAudience audience, Guid? target = null) => new()
    {
        Id = Id(number), Title = $"Notice {number}", Content = $"Content {number}", Audience = audience,
        TargetUserId = target, CreatedAt = Epoch.AddMinutes(-number)
    };
}

public class AdvancingClock : TimeProvider
{
    private long ticks = NotificationReadApiFactory.Epoch.AddDays(1).Ticks;
    public override DateTimeOffset GetUtcNow() => new(Interlocked.Add(ref ticks, TimeSpan.TicksPerSecond), TimeSpan.Zero);
}

// Coordination is test-only; production correctness relies on conditional SQL and the composite PK.
public class ReadRaceControl
{
    public Func<UserNotificationState, CancellationToken, Task>? BeforeInsert { get; set; }
    public Func<UserNotificationState, CancellationToken, Task>? AfterInsert { get; set; }
    public Func<CancellationToken, Task>? BeforeUpdate { get; set; }
    public Func<CancellationToken, Task>? BeforeDelete { get; set; }
    public Func<CancellationToken, Task>? AfterDeleteUpdate { get; set; }
    public ConcurrentQueue<DateTime> SuccessfulDeletes { get; } = new();
    public int DuplicateConflicts;
    public ConcurrentQueue<DateTime> SuccessfulUpdates { get; } = new();
}

public class SqliteMarkReadStore(SqliteReadDbContext db, ReadRaceControl race) : NotificationReadStateStore(db)
{
    protected override async Task<int> DeleteExistingStateAsync(Guid notificationId, Guid userId, DateTime deletedAt,
        CancellationToken ct)
    {
        if (race.BeforeDelete is not null) await race.BeforeDelete(ct);
        var affected = await base.DeleteExistingStateAsync(notificationId, userId, deletedAt, ct);
        if (affected > 0) race.SuccessfulDeletes.Enqueue(deletedAt);
        if (race.AfterDeleteUpdate is not null) await race.AfterDeleteUpdate(ct);
        return affected;
    }

    protected override async Task InsertStateAsync(UserNotificationState state, CancellationToken ct)
    {
        if (race.BeforeInsert is not null) await race.BeforeInsert(state, ct);
        await base.InsertStateAsync(state, ct);
        if (race.AfterInsert is not null) await race.AfterInsert(state, ct);
    }

    protected override async Task<int> MarkExistingStateAsync(Guid notificationId, Guid userId, DateTime readAt, CancellationToken ct)
    {
        if (race.BeforeUpdate is not null) await race.BeforeUpdate(ct);
        var affected = await base.MarkExistingStateAsync(notificationId, userId, readAt, ct);
        if (affected > 0) race.SuccessfulUpdates.Enqueue(readAt);
        return affected;
    }

    protected override bool IsDuplicateStateConflict(DbUpdateException exception, UserNotificationState inserted)
    {
        var expected = exception.Entries.Count == 1 && ReferenceEquals(exception.Entries[0].Entity, inserted) &&
            exception.Entries[0].State == EntityState.Added && exception.InnerException is SqliteException sqlite &&
            sqlite.SqliteErrorCode == 19 && sqlite.SqliteExtendedErrorCode == 1555 &&
            sqlite.Message.Contains("UserNotificationStates.NotificationId, UserNotificationStates.UserId", StringComparison.Ordinal);
        if (expected) Interlocked.Increment(ref race.DuplicateConflicts);
        return expected;
    }
}
