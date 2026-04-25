using System.IO;
using System.Threading.Tasks;

namespace CradleSoft.DMS.Services;

public interface IStorageService
{
    Task UploadObjectAsync(string bucketName, string key, Stream data, string contentType);
    Task<Stream> GetObjectAsync(string bucketName, string key);
    Task<bool> ObjectExistsAsync(string bucketName, string key);
    Task EnsureBucketExistsAsync(string bucketName);
    Task<IEnumerable<string>> ListBucketsAsync();
    Task<IEnumerable<S3ObjectMetadata>> ListObjectsAsync(string bucketName);
    Task DeleteObjectAsync(string bucketName, string key);
    Task DeleteBucketAsync(string bucketName);
    Task<string?> GetBucketPolicyAsync(string bucketName);
    Task SetBucketPolicyAsync(string bucketName, string policyJson);
}

public record S3ObjectMetadata(string Key, long Size, DateTime LastModified, string ContentType);
