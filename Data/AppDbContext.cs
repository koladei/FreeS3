using Microsoft.EntityFrameworkCore;
using CradleSoft.DMS.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace CradleSoft.DMS.Data
{
  public class AppDbContext : IdentityDbContext<ApplicationUser>
  {
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
      
    }

    public DbSet<RefreshToken> RefreshTokens { get; set; }

    public DbSet<AccessToken> RevokedAccessTokens { get; set; }
  }
}
