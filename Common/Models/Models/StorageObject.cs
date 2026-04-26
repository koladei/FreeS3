namespace CradleSoft.DMS.Models;

public class StorageObject
{
    public long Id { get; set; }

    public Guid RouteId { get; set; } = Guid.NewGuid();

    public Guid BucketId { get; set; }

    public string BucketName { get; set; } = string.Empty;

    public string ObjectKey { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string ContentType { get; set; } = "application/octet-stream";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public StorageBucket? Bucket { get; set; }

    public ICollection<ObjectShare> Shares { get; set; } = new List<ObjectShare>();
}
