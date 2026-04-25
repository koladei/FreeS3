using System.ComponentModel.DataAnnotations;

namespace CradleSoft.DMS.Models.Dtos
{
  public class LoginDto
  {
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
  }
}
