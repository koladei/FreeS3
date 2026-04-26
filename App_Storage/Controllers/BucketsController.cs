using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CradleSoft.DMS.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Claims;
using System;
using CradleSoft.DMS.Models;

namespace CradleSoft.DMS.App_Storage.Controllers;

[ApiController]
[Route("s3/[controller]")]
[Authorize]
public class BucketsController : ControllerBase
{
    private readonly IStorageService _storage;

    public BucketsController(IStorageService storage)
    {
        _storage = storage;
    }

    private string GetUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? throw new InvalidOperationException("User ID not found in claims.");
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<string>>> GetBuckets()
    {
        var userId = GetUserId();
        var buckets = await _storage.ListBucketsAsync(userId);
        return Ok(buckets);
    }

    [HttpGet("access")]
    public async Task<ActionResult<IEnumerable<BucketAccessMetadata>>> GetAccessibleBuckets()
    {
        var userId = GetUserId();
        var buckets = await _storage.ListAccessibleBucketsAsync(userId);
        return Ok(buckets);
    }

    [HttpPost("{name}")]
    public async Task<IActionResult> CreateBucket(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("Bucket name is required.");

        if (StorageSystemBuckets.IsMyOutgoingContracts(name))
        {
            return BadRequest($"'{StorageSystemBuckets.MyOutgoingContracts}' is a reserved system bucket.");
        }
        
        var userId = GetUserId();
        await _storage.EnsureBucketExistsAsync(name, userId);
        return Ok();
    }

    [HttpDelete("{name}")]
    public async Task<IActionResult> DeleteBucket(string name)
    {
        if (StorageSystemBuckets.IsMyOutgoingContracts(name))
        {
            return BadRequest($"'{StorageSystemBuckets.MyOutgoingContracts}' bucket cannot be deleted.");
        }

        var userId = GetUserId();

        if (!await _storage.UserOwnsBucketAsync(name, userId))
        {
            return Forbid();
        }

        await _storage.DeleteBucketAsync(userId, name);
        return Ok();
    }

    [HttpGet("{name}/objects")]
    public async Task<ActionResult<IEnumerable<S3ObjectMetadata>>> GetObjects(string name)
    {
        var userId = GetUserId();
        
        // Check if user owns the bucket
        if (!await _storage.UserHasAccessToBucketAsync(name, userId))
        {
            return Forbid();
        }

        var objects = await _storage.ListObjectsAsync(userId, name);
        return Ok(objects);
    }

    [HttpDelete("{name}/objects/{**key}")]
    public async Task<IActionResult> DeleteObject(string name, string key)
    {
        var userId = GetUserId();

        if (!await _storage.CanDeleteFromBucketAsync(name, userId))
        {
            return Forbid();
        }

        await _storage.DeleteObjectAsync(userId, name, key);
        return Ok();
    }

    [HttpGet("{name}/policy")]
    public async Task<ActionResult<string>> GetPolicy(string name)
    {
        var userId = GetUserId();

        if (!await _storage.UserOwnsBucketAsync(name, userId))
        {
            return Forbid();
        }

        var policy = await _storage.GetBucketPolicyAsync(userId, name);
        return Ok(policy ?? "{}");
    }

    [HttpPut("{name}/policy")]
    public async Task<IActionResult> SetPolicy(string name, [FromBody] string policyJson)
    {
        var userId = GetUserId();

        if (!await _storage.UserOwnsBucketAsync(name, userId))
        {
            return Forbid();
        }

        await _storage.SetBucketPolicyAsync(userId, name, policyJson);
        return Ok();
    }

    [HttpGet("{name}/shares")]
    public async Task<ActionResult<IEnumerable<BucketShareMetadata>>> GetBucketShares(string name)
    {
        var userId = GetUserId();
        if (!await _storage.UserOwnsBucketAsync(name, userId))
        {
            return Forbid();
        }

        var shares = await _storage.ListBucketSharesAsync(userId, name);
        return Ok(shares);
    }

    [HttpPost("{name}/shares")]
    public async Task<IActionResult> ShareBucketWithUser(string name, [FromBody] ShareBucketRequest request)
    {
        if (StorageSystemBuckets.IsMyOutgoingContracts(name))
        {
            return BadRequest($"'{StorageSystemBuckets.MyOutgoingContracts}' bucket cannot be shared. Share objects inside it instead.");
        }

        if (request == null || string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest("Email is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Permission))
        {
            return BadRequest("Permission is required. Use ViewOnly, Modify, or ModifyOnly.");
        }

        if (request.ExpiresAt.HasValue && request.ExpiresAt.Value <= DateTimeOffset.UtcNow)
        {
            return BadRequest("Expiration must be in the future.");
        }

        var userId = GetUserId();
        if (!await _storage.UserOwnsBucketAsync(name, userId))
        {
            return Forbid();
        }

        var shared = await _storage.ShareBucketWithUserByEmailAsync(userId, name, request.Email, request.Permission, request.ExpiresAt);
        if (!shared)
        {
            return NotFound("Unable to share bucket. Ensure email exists, permission is valid, and expiration (if set) is in the future.");
        }

        return Ok();
    }

    [HttpDelete("{name}/shares")]
    public async Task<IActionResult> UnshareBucketWithUser(string name, [FromQuery] string email)
    {
        if (StorageSystemBuckets.IsMyOutgoingContracts(name))
        {
            return BadRequest($"'{StorageSystemBuckets.MyOutgoingContracts}' bucket cannot be shared. Share objects inside it instead.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest("Email is required.");
        }

        var userId = GetUserId();
        if (!await _storage.UserOwnsBucketAsync(name, userId))
        {
            return Forbid();
        }

        var removed = await _storage.UnshareBucketWithUserByEmailAsync(userId, name, email);
        if (!removed)
        {
            return NotFound("Share entry not found for the provided email.");
        }

        return Ok();
    }

    [HttpGet("shares/incoming")]
    public async Task<ActionResult<IEnumerable<IncomingBucketShareMetadata>>> GetIncomingShares()
    {
        var userId = GetUserId();
        var shares = await _storage.ListIncomingBucketSharesAsync(userId);
        return Ok(shares);
    }

    [HttpPost("{name}/shares/acknowledge")]
    public async Task<IActionResult> AcknowledgeShare(string name)
    {
        var userId = GetUserId();
        var acknowledged = await _storage.AcknowledgeBucketShareAsync(userId, name);
        if (!acknowledged)
        {
            return NotFound("Share request not found or has expired.");
        }

        return Ok();
    }

    [HttpGet("{name}/object-shares")]
    public async Task<ActionResult<IEnumerable<ObjectShareMetadata>>> GetObjectShares(string name, [FromQuery] string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return BadRequest("Object key is required.");
        }

        var userId = GetUserId();
        if (!await _storage.UserOwnsBucketAsync(name, userId))
        {
            return Forbid();
        }

        var shares = await _storage.ListObjectSharesAsync(userId, name, key);
        return Ok(shares);
    }

    [HttpPost("{name}/object-shares")]
    public async Task<IActionResult> ShareObjectWithUser(string name, [FromBody] ShareObjectRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest("Object key and email are required.");
        }

        if (request.ExpiresAt.HasValue && request.ExpiresAt.Value <= DateTimeOffset.UtcNow)
        {
            return BadRequest("Expiration must be in the future.");
        }

        var userId = GetUserId();
        if (!await _storage.UserOwnsBucketAsync(name, userId))
        {
            return Forbid();
        }

        var shared = await _storage.ShareObjectWithUserByEmailAsync(userId, name, request.Key, request.Email, request.ExpiresAt);
        if (!shared)
        {
            return NotFound("Unable to share object. Ensure object exists, email exists, and expiration (if set) is in the future.");
        }

        return Ok();
    }

    [HttpDelete("{name}/object-shares")]
    public async Task<IActionResult> UnshareObjectWithUser(string name, [FromQuery] string key, [FromQuery] string email)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(email))
        {
            return BadRequest("Object key and email are required.");
        }

        var userId = GetUserId();
        if (!await _storage.UserOwnsBucketAsync(name, userId))
        {
            return Forbid();
        }

        var removed = await _storage.UnshareObjectWithUserByEmailAsync(userId, name, key, email);
        if (!removed)
        {
            return NotFound("Object share entry not found for the provided key/email.");
        }

        return Ok();
    }

    [HttpGet("object-shares/incoming")]
    public async Task<ActionResult<IEnumerable<IncomingObjectShareMetadata>>> GetIncomingObjectShares()
    {
        var userId = GetUserId();
        var shares = await _storage.ListIncomingObjectSharesAsync(userId);
        return Ok(shares);
    }

    public sealed class ShareBucketRequest
    {
        public string Email { get; set; } = string.Empty;

        public string Permission { get; set; } = "ViewOnly";

        public DateTimeOffset? ExpiresAt { get; set; }
    }

    public sealed class ShareObjectRequest
    {
        public string Key { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public DateTimeOffset? ExpiresAt { get; set; }
    }
}
