namespace CradleSoft.DMS.Models
{
  public class ContractInstance
  {
    public string InstanceId { get; set; } = Guid.NewGuid().ToString("N");
    public string TemplateId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string Status { get; set; } = "InProgress";
    public bool IsDigitallySigned { get; set; }
    public string? FinalArtifactHash { get; set; }
    public DateTimeOffset? SignedAt { get; set; }

    public ContractTemplate Template { get; set; } = null!;
    public List<ContractSigner> Signers { get; set; } = [];
    public List<ContractFieldValue> FieldValues { get; set; } = [];
  }
}
