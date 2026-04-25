using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;

namespace CradleSoft.DMS.Data.Helpers
{
  public static class UserRoles
  {
    public const string Admin = "Admin";
    public const string User = "User";
  }
  
  public class AppDbInitializer
  {
    public static async Task SeedRolesToDatabase(WebApplicationBuilder builder)
    {
      using (var serviceScope = builder.Services.BuildServiceProvider().CreateScope())
      {
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
}
