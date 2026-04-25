using System.ComponentModel.DataAnnotations.Schema;

namespace CradleSoft.DMS.Models
{
  public class AccessToken
  {
    public int Id { get; set; }

    public string JwtId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    [ForeignKey("UserId")]
    public ApplicationUser User { get; set; } = null!;
  }
}
