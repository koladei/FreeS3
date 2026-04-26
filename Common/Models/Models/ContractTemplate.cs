namespace CradleSoft.DMS.Models
{
  public class ContractTemplate
  {
    public string TemplateId { get; set; } = Guid.NewGuid().ToString("N");
    public string Bucket { get; set; } = string.Empty;
    public string ObjectKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public string? Title { get; set; }
    public string? AuthorId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string Status { get; set; } = "Draft";

    public List<ContractPlaceholder> Placeholders { get; set; } = [];
    public List<ContractInstance> Instances { get; set; } = [];
  }
}
