using System.IO;
using System.Threading.Tasks;

namespace CradleSoft.DMS.Services;

public interface IStorageService
{
    Task UploadObjectAsync(string userId, string bucketName, string key, Stream data, string contentType);
    Task<Stream> GetObjectAsync(string userId, string bucketName, string key);
    Task<bool> ObjectExistsAsync(string userId, string bucketName, string key);
    Task EnsureBucketExistsAsync(string bucketName, string? ownerId = null);
    Task<IEnumerable<string>> ListBucketsAsync(string userId);
    Task<IEnumerable<BucketAccessMetadata>> ListAccessibleBucketsAsync(string userId);
    Task<IEnumerable<S3ObjectMetadata>> ListObjectsAsync(string userId, string bucketName);
    Task DeleteObjectAsync(string userId, string bucketName, string key);
    Task DeleteBucketAsync(string userId, string bucketName);
    Task<string?> GetBucketPolicyAsync(string userId, string bucketName);
    Task SetBucketPolicyAsync(string userId, string bucketName, string policyJson);
    Task<bool> UserHasAccessToBucketAsync(string bucketName, string userId);
    Task<bool> UserHasAccessToObjectAsync(string bucketName, string key, string userId);
    Task<bool> CanUploadToBucketAsync(string bucketName, string userId);
    Task<bool> CanDeleteFromBucketAsync(string bucketName, string userId);
    Task<bool> UserOwnsBucketAsync(string bucketName, string userId);
    Task<bool> ShareBucketWithUserByEmailAsync(string ownerUserId, string bucketName, string targetEmail, string permission, DateTimeOffset? expiresAt = null);
    Task<bool> UnshareBucketWithUserByEmailAsync(string ownerUserId, string bucketName, string targetEmail);
    Task<IEnumerable<BucketShareMetadata>> ListBucketSharesAsync(string ownerUserId, string bucketName);
    Task<IEnumerable<IncomingBucketShareMetadata>> ListIncomingBucketSharesAsync(string userId);
    Task<bool> AcknowledgeBucketShareAsync(string userId, string bucketName);
    Task<bool> ShareObjectWithUserByEmailAsync(string ownerUserId, string bucketName, string objectKey, string targetEmail, DateTimeOffset? expiresAt = null);
    Task<bool> UnshareObjectWithUserByEmailAsync(string ownerUserId, string bucketName, string objectKey, string targetEmail);
    Task<IEnumerable<ObjectShareMetadata>> ListObjectSharesAsync(string ownerUserId, string bucketName, string objectKey);
    Task<IEnumerable<IncomingObjectShareMetadata>> ListIncomingObjectSharesAsync(string userId);
}

public record S3ObjectMetadata(Guid Id, string Key, long Size, DateTime LastModified, string ContentType);
public record BucketAccessMetadata(Guid Id, string BucketName, string DisplayName, string AccessType, bool IsOwner, string? OwnerEmail, string? SharePermission);
public record BucketShareMetadata(string SharedWithEmail, DateTimeOffset SharedAt, DateTimeOffset? AcknowledgedAt, DateTimeOffset? ExpiresAt, string Permission);
public record IncomingBucketShareMetadata(string BucketName, string SharedByEmail, DateTimeOffset SharedAt, DateTimeOffset? AcknowledgedAt, DateTimeOffset? ExpiresAt, bool IsAcknowledged, string Permission);
public record ObjectShareMetadata(string SharedWithEmail, DateTimeOffset SharedAt, DateTimeOffset? ExpiresAt);
public record IncomingObjectShareMetadata(Guid ObjectId, string BucketName, string ObjectKey, string SharedByEmail, DateTimeOffset SharedAt, DateTimeOffset? ExpiresAt);
