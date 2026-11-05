using Identity.API.Models;
using Microsoft.AspNetCore.Identity;

namespace Identity.API.Data;

public static class IdentityDbInitializer
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<AppUser>>();

        string[] roles = ["Admin", "Learner", "Moderator"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // Seed default accounts if not exist
        await EnsureUserAsync(userManager, "admin@acadprep.com", "System Administrator", "Admin@123456", "Admin");
        await EnsureUserAsync(userManager, "moderator@acadprep.com", "Content Moderator", "Moderator@123456", "Moderator");
        await EnsureUserAsync(userManager, "moderator.demo@example.test", "Demo Moderator", "Demo@123456", "Moderator");
        await EnsureUserAsync(userManager, "learner@acadprep.com", "Default Learner", "Learner@123456", "Learner");
    }

    private static async Task EnsureUserAsync(
        UserManager<AppUser> userManager,
        string email,
        string fullName,
        string password,
        string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new AppUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                Status = UserStatus.Active,
                AuthProvider = "Local",
                EmailConfirmed = true,
                CreatedAt = Common.TimeHelper.Now
            };

            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }
    }
}
