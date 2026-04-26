namespace CradleSoft.DMS.Models
{
  public class ContractSigner
  {
    public int Id { get; set; }
    public string InstanceId { get; set; } = string.Empty;
    public string SignerId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public int RoutingOrder { get; set; }

    public ContractInstance Instance { get; set; } = null!;
  }
}
