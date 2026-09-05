using Microsoft.AspNetCore.Identity;

namespace TmsApi.Infrastructure.Identity;

public static class IdentitySeeder
{
    public static async Task SeedAsync(
        UserManager<TmsUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        const string adminRole = "Admin";

        if (!await roleManager.RoleExistsAsync(adminRole))
        {
            await roleManager.CreateAsync(
                new IdentityRole(adminRole));
        }

        const string adminEmail = "admin@mail.com";

        var existingAdmin =
            await userManager.FindByEmailAsync(adminEmail);

        if (existingAdmin != null)
        {
            if (!await userManager.IsInRoleAsync(
                    existingAdmin,
                    adminRole))
            {
                await userManager.AddToRoleAsync(
                    existingAdmin,
                    adminRole);
            }

            return;
        }

        var admin = new TmsUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true,
            FirstName = "Admin",
            LastName = "Test"
        };

        var result =
            await userManager.CreateAsync(
                admin,
                "Password!123");

        if (!result.Succeeded)
        {
            var errors = string.Join(
                ", ",
                result.Errors.Select(e => e.Description));

            throw new InvalidOperationException(
                $"Failed to create initial Admin: {errors}");
        }

        await userManager.AddToRoleAsync(
            admin,
            adminRole);
    }
}