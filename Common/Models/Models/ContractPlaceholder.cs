namespace CradleSoft.DMS.Models
{
  public class ContractPlaceholder
  {
    public string PlaceholderId { get; set; } = Guid.NewGuid().ToString("N");
    public string TemplateId { get; set; } = string.Empty;
    public string FieldType { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int Page { get; set; }
    public decimal X { get; set; }
    public decimal Y { get; set; }
    public decimal Width { get; set; }
    public decimal Height { get; set; }
    public bool Required { get; set; }
    public string? Label { get; set; }
    public int? MaxLength { get; set; }
    public int? Order { get; set; }

    public ContractTemplate Template { get; set; } = null!;
  }
}
