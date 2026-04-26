namespace CradleSoft.DMS.Models
{
  public class ContractFieldValue
  {
    public int Id { get; set; }
    public string InstanceId { get; set; } = string.Empty;
    public string PlaceholderId { get; set; } = string.Empty;
    public string SignerId { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? SignatureData { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public string? SourceIp { get; set; }

    public ContractInstance Instance { get; set; } = null!;
  }
}
