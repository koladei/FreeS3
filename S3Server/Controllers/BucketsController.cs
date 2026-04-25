using Microsoft.AspNetCore.Mvc;
using CradleSoft.DMS.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CradleSoft.DMS.S3Server.Controllers;

[ApiController]
[Route("S3Server/[controller]")]
public class BucketsController : ControllerBase
{
    private readonly IStorageService _storage;

    public BucketsController(IStorageService storage)
    {
        _storage = storage;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<string>>> GetBuckets()
    {
        var buckets = await _storage.ListBucketsAsync();
        return Ok(buckets);
    }

    [HttpPost("{name}")]
    public async Task<IActionResult> CreateBucket(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("Bucket name is required.");
        
        await _storage.EnsureBucketExistsAsync(name);
        return Ok();
    }

    [HttpDelete("{name}")]
    public async Task<IActionResult> DeleteBucket(string name)
    {
        await _storage.DeleteBucketAsync(name);
        return Ok();
    }

    [HttpGet("{name}/objects")]
    public async Task<ActionResult<IEnumerable<S3ObjectMetadata>>> GetObjects(string name)
    {
        var objects = await _storage.ListObjectsAsync(name);
        return Ok(objects);
    }

    [HttpDelete("{name}/objects/{**key}")]
    public async Task<IActionResult> DeleteObject(string name, string key)
    {
        await _storage.DeleteObjectAsync(name, key);
        return Ok();
    }

    [HttpGet("{name}/policy")]
    public async Task<ActionResult<string>> GetPolicy(string name)
    {
        var policy = await _storage.GetBucketPolicyAsync(name);
        return Ok(policy ?? "{}");
    }

    [HttpPut("{name}/policy")]
    public async Task<IActionResult> SetPolicy(string name, [FromBody] string policyJson)
    {
        await _storage.SetBucketPolicyAsync(name, policyJson);
        return Ok();
    }
}
