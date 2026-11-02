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

        // Seed default Admin if not exists
        var adminEmail = "admin@acadprep.com";
        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            var defaultAdmin = new AppUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "System Administrator",
                Status = UserStatus.Active,
                AuthProvider = "Local",
                EmailConfirmed = true,
                CreatedAt = Common.TimeHelper.Now
            };

            var result = await userManager.CreateAsync(defaultAdmin, "Admin@123456");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(defaultAdmin, "Admin");
            }
        }
    }
}
