namespace CradleSoft.DMS.Models;

public class StorageBucket
{
    public string BucketName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string? PolicyJson { get; set; }

    public long ObjectCount { get; set; }

    public long TotalSizeBytes { get; set; }

    public ICollection<StorageObject> Objects { get; set; } = new List<StorageObject>();
}
