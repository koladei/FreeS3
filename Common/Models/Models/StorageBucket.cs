namespace CradleSoft.DMS.Models;

public class StorageBucket
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string BucketName { get; set; } = string.Empty;

    public string? OwnerId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string? PolicyJson { get; set; }

    public long ObjectCount { get; set; }

    public long TotalSizeBytes { get; set; }

    public ApplicationUser? Owner { get; set; }

    public ICollection<StorageObject> Objects { get; set; } = new List<StorageObject>();

    public ICollection<BucketShare> Shares { get; set; } = new List<BucketShare>();
}
