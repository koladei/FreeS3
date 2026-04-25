using System.ComponentModel.DataAnnotations;

namespace CradleSoft.DMS.Models.Dtos
{
  public class RefreshTokenDto
  {
    public string Token { get; set; } = string.Empty;

    // public DateTime ExpiresAt { get; set; }

    public string RefreshToken { get; set; } = string.Empty;
  }
}
