using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace CradleSoft.DMS.Services;

/// <summary>
/// Implements IStorageService using the local file system as the backing store.
/// Files are organized as: {StorageRoot}/{bucketName}/{key}
/// This mimics the path-style layout used by MinIO and S3.
/// </summary>
public class LocalDiskStorageService : IStorageService
{
    private readonly string _storageRoot;

    public LocalDiskStorageService(IConfiguration configuration)
    {
        // Default to App_Data/s3-emulator next to the API project if not configured.
        _storageRoot = configuration["S3Settings:StorageRoot"]
                       ?? Path.Combine(AppContext.BaseDirectory, "App_Data", "s3-emulator");
        
        if (!Directory.Exists(_storageRoot))
        {
            Directory.CreateDirectory(_storageRoot);
        }
    }

    /// <inheritdoc/>
    public Task EnsureBucketExistsAsync(string bucketName)
    {
        var bucketPath = GetBucketPath(bucketName);
        Directory.CreateDirectory(bucketPath);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task UploadObjectAsync(string bucketName, string key, Stream data, string contentType)
    {
        await EnsureBucketExistsAsync(bucketName);

        var filePath = GetObjectPath(bucketName, key);

        // Ensure any subdirectories within the key exist (e.g., "folder/sub/file.txt")
        var directory = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(directory);

        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await data.CopyToAsync(fileStream);
    }

    /// <inheritdoc/>
    public Task<Stream> GetObjectAsync(string bucketName, string key)
    {
        var filePath = GetObjectPath(bucketName, key);

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Object '{key}' not found in bucket '{bucketName}'.", filePath);

        Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    /// <inheritdoc/>
    public Task<bool> ObjectExistsAsync(string bucketName, string key)
    {
        var filePath = GetObjectPath(bucketName, key);
        return Task.FromResult(File.Exists(filePath));
    }

    /// <inheritdoc/>
    public Task<IEnumerable<string>> ListBucketsAsync()
    {
        if (!Directory.Exists(_storageRoot)) return Task.FromResult(Enumerable.Empty<string>());
        
        var directories = Directory.GetDirectories(_storageRoot)
                                   .Select(Path.GetFileName)
                                   .Where(n => n != null)
                                   .Cast<string>();
        
        return Task.FromResult(directories);
    }

    /// <inheritdoc/>
    public Task<IEnumerable<S3ObjectMetadata>> ListObjectsAsync(string bucketName)
    {
        var bucketPath = GetBucketPath(bucketName);
        if (!Directory.Exists(bucketPath)) return Task.FromResult(Enumerable.Empty<S3ObjectMetadata>());

        var files = Directory.GetFiles(bucketPath, "*", SearchOption.AllDirectories)
                             .Where(f => !Path.GetFileName(f).StartsWith('.')) // Hide internal files like .policy.json
                             .Select(f => {
                                 var info = new FileInfo(f);
                                 var key = Path.GetRelativePath(bucketPath, f).Replace(Path.DirectorySeparatorChar, '/');
                                 return new S3ObjectMetadata(key, info.Length, info.LastWriteTimeUtc, GetContentType(key));
                             });

        return Task.FromResult(files);
    }

    /// <inheritdoc/>
    public Task DeleteObjectAsync(string bucketName, string key)
    {
        var filePath = GetObjectPath(bucketName, key);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DeleteBucketAsync(string bucketName)
    {
        var bucketPath = GetBucketPath(bucketName);
        if (Directory.Exists(bucketPath))
        {
            Directory.Delete(bucketPath, true);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<string?> GetBucketPolicyAsync(string bucketName)
    {
        var policyPath = Path.Combine(GetBucketPath(bucketName), ".policy.json");
        if (!File.Exists(policyPath)) return null;
        return await File.ReadAllTextAsync(policyPath);
    }

    /// <inheritdoc/>
    public async Task SetBucketPolicyAsync(string bucketName, string policyJson)
    {
        await EnsureBucketExistsAsync(bucketName);
        var policyPath = Path.Combine(GetBucketPath(bucketName), ".policy.json");
        await File.WriteAllTextAsync(policyPath, policyJson);
    }

    // --- Helpers ---

    private string GetBucketPath(string bucketName) =>
        Path.Combine(_storageRoot, bucketName);

    private string GetObjectPath(string bucketName, string key) =>
        Path.Combine(_storageRoot, bucketName, key.Replace('/', Path.DirectorySeparatorChar));

    private static string GetContentType(string key) =>
        Path.GetExtension(key).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png"            => "image/png",
            ".gif"            => "image/gif",
            ".pdf"            => "application/pdf",
            ".json"           => "application/json",
            ".txt"            => "text/plain",
            ".xml"            => "application/xml",
            _                 => "application/octet-stream"
        };
}
