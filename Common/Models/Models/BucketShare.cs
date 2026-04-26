namespace CradleSoft.DMS.Models;

public static class BucketSharePermissions
{
    public const string ViewOnly = "ViewOnly";
    public const string Modify = "Modify";
    public const string ModifyOnly = "ModifyOnly";
}

public class BucketShare
{
    public long Id { get; set; }

    public string BucketName { get; set; } = string.Empty;

    public string SharedByUserId { get; set; } = string.Empty;

    public string SharedWithUserId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? AcknowledgedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public string Permission { get; set; } = BucketSharePermissions.ViewOnly;

    public StorageBucket? Bucket { get; set; }

    public ApplicationUser? SharedByUser { get; set; }

    public ApplicationUser? SharedWithUser { get; set; }
}
