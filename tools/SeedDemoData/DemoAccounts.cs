using Identity.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace SeedDemoData;

internal static class DemoAccounts
{
    internal const string Password = "Demo@123456";
    internal static async Task<List<AppUser>> SeedAsync(IServiceProvider sp)
    {
        var manager = sp.GetRequiredService<UserManager<AppUser>>();
        var rows = new (string Id, string Email, string Name, string Role, UserStatus Status, int Age)[]
        {
            ("user-demo-1", "hana@acadprep.com", "Hana Nguyen", "Learner", UserStatus.Active, 60),
            ("user-demo-2", "minh@acadprep.com", "Minh Tran", "Learner", UserStatus.Active, 45),
            ("user-demo-3", "hoang@acadprep.com", "Hoang Nguyen", "Learner", UserStatus.Active, 30),
            ("user-demo-4", "lananh@acadprep.com", "Lan Anh", "Learner", UserStatus.Active, 25),
            ("user-demo-5", "duyliem@acadprep.com", "Duy Liem", "Learner", UserStatus.Active, 20),
            ("user-demo-6", "tuananh@acadprep.com", "Tuan Anh", "Learner", UserStatus.Active, 15),
            ("moderator-demo", "moderator.demo@example.test", "Demo Moderator", "Moderator", UserStatus.Active, 40),
            ("admin-demo", "admin.demo@example.test", "Demo Administrator", "Admin", UserStatus.Active, 50),
            ("banned-demo", "banned.demo@example.test", "Demo Banned Learner", "Learner", UserStatus.Banned, 10),
            ("pending-demo", "pending.demo@example.test", "Demo Pending Learner", "Learner", UserStatus.PendingVerification, 1)
        };
        var learners = new List<AppUser>();
        foreach (var row in rows)
        {
            var user = await manager.FindByEmailAsync(row.Email);
            if (user is null)
            {
                if (await manager.FindByIdAsync(row.Id) is not null)
                    throw new InvalidOperationException($"Demo ID {row.Id} is already used by another account; no account was overwritten.");
                user = new AppUser
                {
                    Id = row.Id, Email = row.Email, UserName = row.Email, FullName = row.Name,
                    Status = row.Status, AuthProvider = "Local",
                    EmailConfirmed = row.Status != UserStatus.PendingVerification,
                    CreatedAt = DateTime.UtcNow.AddDays(-row.Age),
                    LastLoginAt = row.Status == UserStatus.Active ? DateTime.UtcNow.AddDays(-1) : null
                };
                Check(await manager.CreateAsync(user, Password));
                Check(await manager.AddToRoleAsync(user, row.Role));
            }
            // Existing accounts keep their password, roles, status, and profile. Cross-service data uses their real ID.
            if (row.Id.StartsWith("user-demo-")) learners.Add(user);
        }
        return learners;
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
