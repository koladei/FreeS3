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
        var ownerId = await ResolveBucketOwnerIdAsync(bucketName);
        if (ownerId == null)
        {
            await EnsureBucketExistsAsync(bucketName, userId);
            ownerId = userId;
        }
        else if (!await CanUploadToBucketAsync(bucketName, userId))
        {
            throw new UnauthorizedAccessException($"User '{userId}' cannot upload to bucket '{bucketName}'.");
        }

        var filePath = GetObjectPath(ownerId, bucketName, key);
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

    public async Task<Stream> GetObjectAsync(string userId, string bucketName, string key)
    {
        var ownerId = await ResolveBucketOwnerIdAsync(bucketName)
            ?? throw new FileNotFoundException($"Bucket '{bucketName}' does not exist.");

        var filePath = GetObjectPath(ownerId, bucketName, key);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Object '{key}' not found in bucket '{bucketName}'.", filePath);
        }

        Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return stream;
    }

    public Task<bool> ObjectExistsAsync(string userId, string bucketName, string key) =>
        _dbContext.StorageObjects.AnyAsync(x =>
            x.BucketName == bucketName
            && x.ObjectKey == key
            && x.Bucket != null
            && (x.Bucket.OwnerId == userId
                || x.Bucket.Shares.Any(s =>
                    s.SharedWithUserId == userId
                    && s.AcknowledgedAt != null
                    && (s.ExpiresAt == null || s.ExpiresAt > DateTimeOffset.UtcNow))));

    public async Task<IEnumerable<string>> ListBucketsAsync(string userId)
    {
        var userBuckets = await _dbContext.StorageBuckets
            .Where(x => x.OwnerId == userId)
            .OrderBy(x => x.BucketName)
            .Select(x => x.BucketName)
            .ToListAsync();

        return userBuckets;
    }

    public async Task<IEnumerable<BucketAccessMetadata>> ListAccessibleBucketsAsync(string userId)
    {
        var buckets = await _dbContext.StorageBuckets
            .AsNoTracking()
            .Where(x => x.OwnerId == userId || x.Shares.Any(s =>
                s.SharedWithUserId == userId
                && s.AcknowledgedAt != null
                && (s.ExpiresAt == null || s.ExpiresAt > DateTimeOffset.UtcNow)))
            .OrderBy(x => x.BucketName)
            .Select(x => new BucketAccessMetadata(
                x.BucketName,
                x.OwnerId == userId ? "owned" : "shared",
                x.OwnerId == userId,
                x.Owner != null ? x.Owner.Email : null,
                x.OwnerId == userId
                    ? null
                    : x.Shares
                        .Where(s => s.SharedWithUserId == userId
                            && s.AcknowledgedAt != null
                            && (s.ExpiresAt == null || s.ExpiresAt > DateTimeOffset.UtcNow))
                        .Select(s => s.Permission)
                        .FirstOrDefault()))
            .ToListAsync();

        return buckets;
    }

    public async Task<IEnumerable<S3ObjectMetadata>> ListObjectsAsync(string userId, string bucketName)
    {
        var objects = await _dbContext.StorageObjects
            .Where(x => x.BucketName == bucketName
                && x.Bucket != null
                && (x.Bucket.OwnerId == userId
                    || x.Bucket.Shares.Any(s =>
                        s.SharedWithUserId == userId
                        && s.AcknowledgedAt != null
                        && (s.ExpiresAt == null || s.ExpiresAt > DateTimeOffset.UtcNow))))
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
        if (!await CanDeleteFromBucketAsync(bucketName, userId))
        {
            throw new UnauthorizedAccessException($"User '{userId}' cannot delete objects from bucket '{bucketName}'.");
        }

        var ownerId = await ResolveBucketOwnerIdAsync(bucketName)
            ?? throw new DirectoryNotFoundException($"Bucket '{bucketName}' does not exist.");

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

        var filePath = GetObjectPath(ownerId, bucketName, key);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    public async Task DeleteBucketAsync(string userId, string bucketName)
    {
        var ownerId = await ResolveBucketOwnerIdAsync(bucketName)
            ?? throw new DirectoryNotFoundException($"Bucket '{bucketName}' does not exist.");

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

        var bucketPath = GetBucketPath(ownerId, bucketName);
        if (Directory.Exists(bucketPath))
        {
            Directory.Delete(bucketPath, true);
        }
    }

    public async Task<string?> GetBucketPolicyAsync(string userId, string bucketName)
    {
        var ownerId = await ResolveBucketOwnerIdAsync(bucketName)
            ?? throw new DirectoryNotFoundException($"Bucket '{bucketName}' does not exist.");

        var policyPath = Path.Combine(GetBucketPath(ownerId, bucketName), ".policy.json");
        if (!File.Exists(policyPath))
        {
            return null;
        }

        return await File.ReadAllTextAsync(policyPath);
    }

    public async Task SetBucketPolicyAsync(string userId, string bucketName, string policyJson)
    {
        await EnsureBucketExistsAsync(bucketName, userId);

        var ownerId = await ResolveBucketOwnerIdAsync(bucketName)
            ?? throw new DirectoryNotFoundException($"Bucket '{bucketName}' does not exist.");

        var policyPath = Path.Combine(GetBucketPath(ownerId, bucketName), ".policy.json");
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
        return await _dbContext.StorageBuckets.AnyAsync(x =>
            x.BucketName == bucketName
            && (x.OwnerId == userId || x.Shares.Any(s =>
                s.SharedWithUserId == userId
                && s.AcknowledgedAt != null
                && (s.ExpiresAt == null || s.ExpiresAt > DateTimeOffset.UtcNow))));
    }

    public async Task<bool> CanUploadToBucketAsync(string bucketName, string userId)
    {
        return await _dbContext.StorageBuckets.AnyAsync(x =>
            x.BucketName == bucketName
            && (x.OwnerId == userId || x.Shares.Any(s =>
                s.SharedWithUserId == userId
                && s.AcknowledgedAt != null
                && (s.ExpiresAt == null || s.ExpiresAt > DateTimeOffset.UtcNow)
                && (s.Permission == BucketSharePermissions.Modify || s.Permission == BucketSharePermissions.ModifyOnly))));
    }

    public async Task<bool> CanDeleteFromBucketAsync(string bucketName, string userId)
    {
        return await _dbContext.StorageBuckets.AnyAsync(x =>
            x.BucketName == bucketName
            && (x.OwnerId == userId || x.Shares.Any(s =>
                s.SharedWithUserId == userId
                && s.AcknowledgedAt != null
                && (s.ExpiresAt == null || s.ExpiresAt > DateTimeOffset.UtcNow)
                && s.Permission == BucketSharePermissions.Modify)));
    }

    public Task<bool> UserOwnsBucketAsync(string bucketName, string userId) =>
        _dbContext.StorageBuckets.AnyAsync(x => x.BucketName == bucketName && x.OwnerId == userId);

    public async Task<bool> ShareBucketWithUserByEmailAsync(string ownerUserId, string bucketName, string targetEmail, string permission, DateTimeOffset? expiresAt = null)
    {
        if (expiresAt.HasValue && expiresAt.Value <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        var normalizedPermission = NormalizePermission(permission);
        if (normalizedPermission == null)
        {
            return false;
        }

        var normalizedEmail = targetEmail.Trim().ToUpperInvariant();
        var ownerMatches = await UserOwnsBucketAsync(bucketName, ownerUserId);
        if (!ownerMatches)
        {
            return false;
        }

        var targetUser = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail);

        if (targetUser == null || targetUser.Id == ownerUserId)
        {
            return false;
        }

        var existingShare = await _dbContext.BucketShares.FirstOrDefaultAsync(x =>
            x.BucketName == bucketName && x.SharedWithUserId == targetUser.Id);

        if (existingShare != null)
        {
            // Re-sharing requires fresh recipient acknowledgement.
            existingShare.CreatedAt = DateTimeOffset.UtcNow;
            existingShare.AcknowledgedAt = null;
            existingShare.ExpiresAt = expiresAt;
            existingShare.Permission = normalizedPermission;
            await _dbContext.SaveChangesAsync();
            return true;
        }

        _dbContext.BucketShares.Add(new BucketShare
        {
            BucketName = bucketName,
            SharedByUserId = ownerUserId,
            SharedWithUserId = targetUser.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            AcknowledgedAt = null,
            ExpiresAt = expiresAt,
            Permission = normalizedPermission
        });

        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UnshareBucketWithUserByEmailAsync(string ownerUserId, string bucketName, string targetEmail)
    {
        var normalizedEmail = targetEmail.Trim().ToUpperInvariant();
        var ownerMatches = await UserOwnsBucketAsync(bucketName, ownerUserId);
        if (!ownerMatches)
        {
            return false;
        }

        var targetUser = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail);

        if (targetUser == null)
        {
            return false;
        }

        var share = await _dbContext.BucketShares.FirstOrDefaultAsync(x =>
            x.BucketName == bucketName && x.SharedWithUserId == targetUser.Id);
        if (share == null)
        {
            return false;
        }

        _dbContext.BucketShares.Remove(share);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<BucketShareMetadata>> ListBucketSharesAsync(string ownerUserId, string bucketName)
    {
        var ownerMatches = await UserOwnsBucketAsync(bucketName, ownerUserId);
        if (!ownerMatches)
        {
            return Enumerable.Empty<BucketShareMetadata>();
        }

        var shares = await _dbContext.BucketShares
            .Where(x => x.BucketName == bucketName
                && x.SharedWithUser != null
                && x.SharedWithUser.Email != null
                && x.SharedWithUser.Email != string.Empty)
            .OrderBy(x => x.SharedWithUser!.Email)
            .Select(x => new BucketShareMetadata(
                x.SharedWithUser!.Email!,
                x.CreatedAt,
                x.AcknowledgedAt,
                x.ExpiresAt,
                x.Permission))
            .ToListAsync();

        return shares;
    }

    public async Task<IEnumerable<IncomingBucketShareMetadata>> ListIncomingBucketSharesAsync(string userId)
    {
        var now = DateTimeOffset.UtcNow;
        var incoming = await _dbContext.BucketShares
            .AsNoTracking()
            .Where(x => x.SharedWithUserId == userId && (x.ExpiresAt == null || x.ExpiresAt > now))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new IncomingBucketShareMetadata(
                x.BucketName,
                x.SharedByUser != null ? x.SharedByUser.Email ?? "Unknown" : "Unknown",
                x.CreatedAt,
                x.AcknowledgedAt,
                x.ExpiresAt,
                x.AcknowledgedAt != null,
                x.Permission))
            .ToListAsync();

        return incoming;
    }

    public async Task<bool> AcknowledgeBucketShareAsync(string userId, string bucketName)
    {
        var now = DateTimeOffset.UtcNow;
        var share = await _dbContext.BucketShares.FirstOrDefaultAsync(x =>
            x.BucketName == bucketName
            && x.SharedWithUserId == userId
            && (x.ExpiresAt == null || x.ExpiresAt > now));

        if (share == null)
        {
            return false;
        }

        if (share.AcknowledgedAt == null)
        {
            share.AcknowledgedAt = now;
            await _dbContext.SaveChangesAsync();
        }

        return true;
    }

    private string GetBucketPath(string userId, string bucketName) =>
        Path.Combine(_storageRoot, userId, bucketName);

    private string GetObjectPath(string userId, string bucketName, string key) =>
        Path.Combine(_storageRoot, userId, bucketName, key.Replace('/', Path.DirectorySeparatorChar));

    private async Task<string?> ResolveBucketOwnerIdAsync(string bucketName)
    {
        return await _dbContext.StorageBuckets
            .Where(x => x.BucketName == bucketName)
            .Select(x => x.OwnerId)
            .FirstOrDefaultAsync();
    }

    private static string? NormalizePermission(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            return null;
        }

        return permission.Trim().ToLowerInvariant() switch
        {
            "viewonly" => BucketSharePermissions.ViewOnly,
            "modify" => BucketSharePermissions.Modify,
            "modifyonly" => BucketSharePermissions.ModifyOnly,
            _ => null
        };
    }

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
