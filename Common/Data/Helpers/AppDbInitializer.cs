using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CradleSoft.DMS.Data.Helpers
{
  public static class UserRoles
  {
    public const string Admin = "Admin";
    public const string User = "User";
  }

  public static class AppDbInitializer
  {
    public static async Task SeedRolesToDatabase(IServiceCollection services)
    {
      using var serviceScope = services.BuildServiceProvider().CreateScope();
      var roleManager = serviceScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

      if (!await roleManager.RoleExistsAsync(UserRoles.Admin))
      {
        await roleManager.CreateAsync(new IdentityRole(UserRoles.Admin));
      }

      if (!await roleManager.RoleExistsAsync(UserRoles.User))
      {
        await roleManager.CreateAsync(new IdentityRole(UserRoles.User));
      }
    }
  }
}
