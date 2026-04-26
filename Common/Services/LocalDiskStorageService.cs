using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CradleSoft.DMS.Data;
using CradleSoft.DMS.Models;
using Microsoft.EntityFrameworkCore;
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
    private readonly AppDbContext _dbContext;

    public LocalDiskStorageService(IConfiguration configuration, AppDbContext dbContext)
    {
        _storageRoot = configuration["S3Settings:StorageRoot"]
            ?? Path.Combine(AppContext.BaseDirectory, "App_Data", "s3-emulator");

        if (!Directory.Exists(_storageRoot))
        {
            Directory.CreateDirectory(_storageRoot);
        }

        _dbContext = dbContext;
    }

    public Task EnsureBucketExistsAsync(string bucketName)
    {
        var bucketPath = GetBucketPath(bucketName);
        Directory.CreateDirectory(bucketPath);
        return EnsureBucketMetadataAsync(bucketName);
    }

    public async Task UploadObjectAsync(string bucketName, string key, Stream data, string contentType)
    {
        await EnsureBucketExistsAsync(bucketName);

        var filePath = GetObjectPath(bucketName, key);
        var directory = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(directory);

        await using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await data.CopyToAsync(fileStream);
        }

        var now = DateTimeOffset.UtcNow;
        var fileInfo = new FileInfo(filePath);
        var existingObject = await _dbContext.StorageObjects
            .FirstOrDefaultAsync(x => x.BucketName == bucketName && x.ObjectKey == key);

        if (existingObject == null)
        {
            _dbContext.StorageObjects.Add(new StorageObject
            {
                BucketName = bucketName,
                ObjectKey = key,
                SizeBytes = fileInfo.Length,
                ContentType = contentType,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            existingObject.SizeBytes = fileInfo.Length;
            existingObject.ContentType = contentType;
            existingObject.UpdatedAt = now;
        }

        await RecalculateBucketStatsAsync(bucketName, now);
        await _dbContext.SaveChangesAsync();
    }

    public Task<Stream> GetObjectAsync(string bucketName, string key)
    {
        var filePath = GetObjectPath(bucketName, key);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Object '{key}' not found in bucket '{bucketName}'.", filePath);
        }

        Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task<bool> ObjectExistsAsync(string bucketName, string key)
    {
        var filePath = GetObjectPath(bucketName, key);
        return Task.FromResult(File.Exists(filePath));
    }

    public async Task<IEnumerable<string>> ListBucketsAsync()
    {
        if (!Directory.Exists(_storageRoot))
        {
            return Enumerable.Empty<string>();
        }

        var directories = Directory.GetDirectories(_storageRoot)
            .Select(Path.GetFileName)
            .Where(n => n != null)
            .Cast<string>()
            .ToList();

        var now = DateTimeOffset.UtcNow;
        foreach (var bucketName in directories)
        {
            await EnsureBucketMetadataAsync(bucketName, now, saveChanges: false);
        }

        var staleBuckets = await _dbContext.StorageBuckets
            .Where(x => !directories.Contains(x.BucketName))
            .ToListAsync();

        if (staleBuckets.Count > 0)
        {
            _dbContext.StorageBuckets.RemoveRange(staleBuckets);
        }

        await _dbContext.SaveChangesAsync();

        return directories;
    }

    public Task<IEnumerable<S3ObjectMetadata>> ListObjectsAsync(string bucketName)
    {
        var bucketPath = GetBucketPath(bucketName);
        if (!Directory.Exists(bucketPath))
        {
            return Task.FromResult(Enumerable.Empty<S3ObjectMetadata>());
        }

        var files = Directory.GetFiles(bucketPath, "*", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).StartsWith('.'))
            .Select(f =>
            {
                var info = new FileInfo(f);
                var key = Path.GetRelativePath(bucketPath, f).Replace(Path.DirectorySeparatorChar, '/');
                return new S3ObjectMetadata(key, info.Length, info.LastWriteTimeUtc, GetContentType(key));
            })
            .ToList();

        return SyncObjectsAndReturn(bucketName, files);
    }

    public Task DeleteObjectAsync(string bucketName, string key)
    {
        var filePath = GetObjectPath(bucketName, key);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return DeleteObjectMetadataAsync(bucketName, key);
    }

    public Task DeleteBucketAsync(string bucketName)
    {
        var bucketPath = GetBucketPath(bucketName);
        if (Directory.Exists(bucketPath))
        {
            Directory.Delete(bucketPath, true);
        }

        return DeleteBucketMetadataAsync(bucketName);
    }

    public async Task<string?> GetBucketPolicyAsync(string bucketName)
    {
        var policyPath = Path.Combine(GetBucketPath(bucketName), ".policy.json");
        if (!File.Exists(policyPath))
        {
            return null;
        }

        return await File.ReadAllTextAsync(policyPath);
    }

    public async Task SetBucketPolicyAsync(string bucketName, string policyJson)
    {
        await EnsureBucketExistsAsync(bucketName);
        var policyPath = Path.Combine(GetBucketPath(bucketName), ".policy.json");
        await File.WriteAllTextAsync(policyPath, policyJson);

        var now = DateTimeOffset.UtcNow;
        var bucket = await _dbContext.StorageBuckets.FirstOrDefaultAsync(x => x.BucketName == bucketName);
        if (bucket != null)
        {
            bucket.PolicyJson = policyJson;
            bucket.UpdatedAt = now;
            await _dbContext.SaveChangesAsync();
        }
    }

    private string GetBucketPath(string bucketName) =>
        Path.Combine(_storageRoot, bucketName);

    private string GetObjectPath(string bucketName, string key) =>
        Path.Combine(_storageRoot, bucketName, key.Replace('/', Path.DirectorySeparatorChar));

    private static string GetContentType(string key) =>
        Path.GetExtension(key).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".pdf" => "application/pdf",
            ".json" => "application/json",
            ".txt" => "text/plain",
            ".xml" => "application/xml",
            _ => "application/octet-stream"
        };

    private async Task EnsureBucketMetadataAsync(string bucketName, DateTimeOffset? now = null, bool saveChanges = true)
    {
        var timestamp = now ?? DateTimeOffset.UtcNow;
        var existingBucket = await _dbContext.StorageBuckets.FirstOrDefaultAsync(x => x.BucketName == bucketName);
        if (existingBucket == null)
        {
            _dbContext.StorageBuckets.Add(new StorageBucket
            {
                BucketName = bucketName,
                CreatedAt = timestamp,
                UpdatedAt = timestamp,
                ObjectCount = 0,
                TotalSizeBytes = 0
            });
        }

        if (saveChanges)
        {
            await _dbContext.SaveChangesAsync();
        }
    }

    private async Task RecalculateBucketStatsAsync(string bucketName, DateTimeOffset timestamp)
    {
        await EnsureBucketMetadataAsync(bucketName, timestamp, saveChanges: false);

        var bucket = await _dbContext.StorageBuckets.FirstAsync(x => x.BucketName == bucketName);
        var objects = await _dbContext.StorageObjects
            .Where(x => x.BucketName == bucketName)
            .ToListAsync();

        bucket.ObjectCount = objects.Count;
        bucket.TotalSizeBytes = objects.Sum(x => x.SizeBytes);
        bucket.UpdatedAt = timestamp;
    }

    private async Task<IEnumerable<S3ObjectMetadata>> SyncObjectsAndReturn(string bucketName, IReadOnlyList<S3ObjectMetadata> files)
    {
        var now = DateTimeOffset.UtcNow;
        await EnsureBucketMetadataAsync(bucketName, now, saveChanges: false);

        var existingObjects = await _dbContext.StorageObjects
            .Where(x => x.BucketName == bucketName)
            .ToDictionaryAsync(x => x.ObjectKey, StringComparer.Ordinal);

        var fileKeys = new HashSet<string>(files.Select(x => x.Key), StringComparer.Ordinal);

        foreach (var file in files)
        {
            if (!existingObjects.TryGetValue(file.Key, out var existing))
            {
                _dbContext.StorageObjects.Add(new StorageObject
                {
                    BucketName = bucketName,
                    ObjectKey = file.Key,
                    SizeBytes = file.Size,
                    ContentType = file.ContentType,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                continue;
            }

            existing.SizeBytes = file.Size;
            existing.ContentType = file.ContentType;
            existing.UpdatedAt = now;
        }

        var missingFromDisk = existingObjects.Values
            .Where(x => !fileKeys.Contains(x.ObjectKey))
            .ToList();
        if (missingFromDisk.Count > 0)
        {
            _dbContext.StorageObjects.RemoveRange(missingFromDisk);
        }

        await RecalculateBucketStatsAsync(bucketName, now);
        await _dbContext.SaveChangesAsync();

        return files;
    }

    private async Task DeleteObjectMetadataAsync(string bucketName, string key)
    {
        var metadata = await _dbContext.StorageObjects
            .FirstOrDefaultAsync(x => x.BucketName == bucketName && x.ObjectKey == key);
        if (metadata != null)
        {
            _dbContext.StorageObjects.Remove(metadata);
        }

        await RecalculateBucketStatsAsync(bucketName, DateTimeOffset.UtcNow);
        await _dbContext.SaveChangesAsync();
    }

    private async Task DeleteBucketMetadataAsync(string bucketName)
    {
        var bucket = await _dbContext.StorageBuckets.FirstOrDefaultAsync(x => x.BucketName == bucketName);
        if (bucket != null)
        {
            _dbContext.StorageBuckets.Remove(bucket);
            await _dbContext.SaveChangesAsync();
        }
    }
}
