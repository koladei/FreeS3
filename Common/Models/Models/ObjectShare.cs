namespace CradleSoft.DMS.Models;

public class ObjectShare
{
    public long Id { get; set; }

    public long StorageObjectId { get; set; }

    public string BucketName { get; set; } = string.Empty;

    public string ObjectKey { get; set; } = string.Empty;

    public string SharedByUserId { get; set; } = string.Empty;

    public string SharedWithUserId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public StorageObject? StorageObject { get; set; }

    public ApplicationUser? SharedByUser { get; set; }

    public ApplicationUser? SharedWithUser { get; set; }
}
