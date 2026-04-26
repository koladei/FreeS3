using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CradleSoft.DMS.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Claims;

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

    [HttpPost("{name}")]
    public async Task<IActionResult> CreateBucket(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("Bucket name is required.");
        
        var userId = GetUserId();
        await _storage.EnsureBucketExistsAsync(name, userId);
        return Ok();
    }

    [HttpDelete("{name}")]
    public async Task<IActionResult> DeleteBucket(string name)
    {
        var userId = GetUserId();
        
        // Check if user owns the bucket
        if (!await _storage.UserHasAccessToBucketAsync(name, userId))
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
        
        // Check if user owns the bucket
        if (!await _storage.UserHasAccessToBucketAsync(name, userId))
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
        
        // Check if user owns the bucket
        if (!await _storage.UserHasAccessToBucketAsync(name, userId))
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
        
        // Check if user owns the bucket
        if (!await _storage.UserHasAccessToBucketAsync(name, userId))
        {
            return Forbid();
        }

        await _storage.SetBucketPolicyAsync(userId, name, policyJson);
        return Ok();
    }
}
