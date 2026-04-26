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
/// Files are organized as: {StorageRoot}/{userId}/{bucketName}/{key}
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

    public Task EnsureBucketExistsAsync(string bucketName, string? ownerId = null)
    {
        if (ownerId == null)
        {
            throw new ArgumentNullException(nameof(ownerId), "Owner ID is required to create a bucket.");
        }
        
        var bucketPath = GetBucketPath(ownerId, bucketName);
        Directory.CreateDirectory(bucketPath);
        return EnsureBucketMetadataAsync(bucketName, ownerId: ownerId);
    }

    public async Task UploadObjectAsync(string userId, string bucketName, string key, Stream data, string contentType)
    {
        await EnsureBucketExistsAsync(bucketName, userId);

        var filePath = GetObjectPath(userId, bucketName, key);
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

    public Task<Stream> GetObjectAsync(string userId, string bucketName, string key)
    {
        var filePath = GetObjectPath(userId, bucketName, key);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Object '{key}' not found in bucket '{bucketName}'.", filePath);
        }

        Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task<bool> ObjectExistsAsync(string userId, string bucketName, string key) =>
        _dbContext.StorageObjects.AnyAsync(x =>
            x.BucketName == bucketName
            && x.ObjectKey == key
            && x.Bucket != null
            && x.Bucket.OwnerId == userId);

    public async Task<IEnumerable<string>> ListBucketsAsync(string userId)
    {
        var userBuckets = await _dbContext.StorageBuckets
            .Where(x => x.OwnerId == userId)
            .OrderBy(x => x.BucketName)
            .Select(x => x.BucketName)
            .ToListAsync();

        return userBuckets;
    }

    public async Task<IEnumerable<S3ObjectMetadata>> ListObjectsAsync(string userId, string bucketName)
    {
        var objects = await _dbContext.StorageObjects
            .Where(x => x.BucketName == bucketName
                && x.Bucket != null
                && x.Bucket.OwnerId == userId)
            .OrderBy(x => x.ObjectKey)
            .Select(x => new S3ObjectMetadata(
                x.ObjectKey,
                x.SizeBytes,
                x.UpdatedAt.UtcDateTime,
                x.ContentType))
            .ToListAsync();

        return objects;
    }

    public async Task DeleteObjectAsync(string userId, string bucketName, string key)
    {
        await using (var transaction = await _dbContext.Database.BeginTransactionAsync())
        {
            var metadata = await _dbContext.StorageObjects
                .FirstOrDefaultAsync(x => x.BucketName == bucketName && x.ObjectKey == key);

            if (metadata != null)
            {
                _dbContext.StorageObjects.Remove(metadata);
                await RecalculateBucketStatsAsync(bucketName, DateTimeOffset.UtcNow);
            }

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        var filePath = GetObjectPath(userId, bucketName, key);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    public async Task DeleteBucketAsync(string userId, string bucketName)
    {
        await using (var transaction = await _dbContext.Database.BeginTransactionAsync())
        {
            var bucket = await _dbContext.StorageBuckets
                .FirstOrDefaultAsync(x => x.BucketName == bucketName);

            if (bucket != null)
            {
                _dbContext.StorageBuckets.Remove(bucket);
            }

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        var bucketPath = GetBucketPath(userId, bucketName);
        if (Directory.Exists(bucketPath))
        {
            Directory.Delete(bucketPath, true);
        }
    }

    public async Task<string?> GetBucketPolicyAsync(string userId, string bucketName)
    {
        var policyPath = Path.Combine(GetBucketPath(userId, bucketName), ".policy.json");
        if (!File.Exists(policyPath))
        {
            return null;
        }

        return await File.ReadAllTextAsync(policyPath);
    }

    public async Task SetBucketPolicyAsync(string userId, string bucketName, string policyJson)
    {
        await EnsureBucketExistsAsync(bucketName, userId);
        var policyPath = Path.Combine(GetBucketPath(userId, bucketName), ".policy.json");
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

    public async Task<bool> UserHasAccessToBucketAsync(string bucketName, string userId)
    {
        var bucket = await _dbContext.StorageBuckets
            .FirstOrDefaultAsync(x => x.BucketName == bucketName);
        
        if (bucket == null)
        {
            return false;
        }

        // User has access if they own the bucket
        return bucket.OwnerId == userId;
    }

    private string GetBucketPath(string userId, string bucketName) =>
        Path.Combine(_storageRoot, userId, bucketName);

    private string GetObjectPath(string userId, string bucketName, string key) =>
        Path.Combine(_storageRoot, userId, bucketName, key.Replace('/', Path.DirectorySeparatorChar));

    private async Task EnsureBucketMetadataAsync(string bucketName, DateTimeOffset? now = null, bool saveChanges = true, string? ownerId = null)
    {
        var timestamp = now ?? DateTimeOffset.UtcNow;
        var existingBucket = await _dbContext.StorageBuckets.FirstOrDefaultAsync(x => x.BucketName == bucketName);
        if (existingBucket == null)
        {
            _dbContext.StorageBuckets.Add(new StorageBucket
            {
                BucketName = bucketName,
                OwnerId = ownerId,
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

}
