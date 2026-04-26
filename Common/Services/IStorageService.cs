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
    Task<IEnumerable<S3ObjectMetadata>> ListObjectsAsync(string userId, string bucketName);
    Task DeleteObjectAsync(string userId, string bucketName, string key);
    Task DeleteBucketAsync(string userId, string bucketName);
    Task<string?> GetBucketPolicyAsync(string userId, string bucketName);
    Task SetBucketPolicyAsync(string userId, string bucketName, string policyJson);
    Task<bool> UserHasAccessToBucketAsync(string bucketName, string userId);
}

public record S3ObjectMetadata(string Key, long Size, DateTime LastModified, string ContentType);
