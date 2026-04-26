using Microsoft.AspNetCore.Mvc;
using CradleSoft.DMS.Services;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.StaticFiles;
using System.Security.Claims;

namespace CradleSoft.DMS.App_Storage.Controllers;

/// <summary>
/// A minimal S3-compatible HTTP App_Storage emulator.
/// Supports path-style access: /{bucketName}/{*key}
///
/// Compatible operations:
///   PUT  /{bucket}/{key}  - Upload an object (raw body stream, like the real S3 SDK sends)
///   GET  /{bucket}/{key}  - Download an object
///   HEAD /{bucket}/{key}  - Check if an object exists
/// </summary>
[ApiController]
[Route("s3/{bucketName}/{**key}")]
[Authorize]
public class ObjectsController : ControllerBase
{
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();
    private readonly IStorageService _storage;

    public ObjectsController(IStorageService storage)
    {
        _storage = storage;
    }

    private string GetUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? throw new InvalidOperationException("User ID not found in claims.");
    }

    /// <summary>
    /// PUT /{bucketName}/{key} - Stores an object. Reads the raw request body, just like S3.
    /// Returns an ETag header (MD5 of the body) which the AWS SDK uses to confirm the upload.
    /// </summary>
    [HttpPut]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> PutObject(string bucketName, string key)
    {
        var userId = GetUserId();
        
        // Check if user owns the bucket
        if (!await _storage.UserHasAccessToBucketAsync(bucketName, userId))
        {
            return Forbid();
        }

        using var memoryStream = new MemoryStream();
        await Request.Body.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        await _storage.UploadObjectAsync(userId, bucketName, key, memoryStream, 
            Request.ContentType ?? "application/octet-stream");

        // Compute MD5 ETag — required for AWS SDK compatibility
        memoryStream.Position = 0;
        var etag = $"\"{ComputeMd5(memoryStream)}\"";
        Response.Headers["ETag"] = etag;

        return Ok();
    }

    /// <summary>
    /// GET /{bucketName}/{key} - Downloads an object by streaming it from disk.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetObject(string bucketName, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return NotFound(S3ErrorXml("NoSuchKey", "The specified key does not exist.", key ?? string.Empty));
        }

        var userId = GetUserId();
        
        // Check if user owns the bucket
        if (!await _storage.UserHasAccessToBucketAsync(bucketName, userId))
        {
            return Forbid();
        }

        try
        {
            var stream = await _storage.GetObjectAsync(userId, bucketName, key);
            var contentType = GetContentType(key);
            return File(stream, contentType, enableRangeProcessing: true);
        }
        catch (FileNotFoundException)
        {
            return NotFound(S3ErrorXml("NoSuchKey", $"The specified key '{key}' does not exist.", key));
        }
    }

    /// <summary>
    /// HEAD /{bucketName}/{key} - Check existence of an object without downloading it.
    /// </summary>
    [HttpHead]
    public async Task<IActionResult> HeadObject(string bucketName, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return NotFound();
        }

        var userId = GetUserId();
        
        // Check if user owns the bucket
        if (!await _storage.UserHasAccessToBucketAsync(bucketName, userId))
        {
            return Forbid();
        }

        var exists = await _storage.ObjectExistsAsync(userId, bucketName, key);
        return exists ? Ok() : NotFound();
    }

    // --- Helpers ---

    private static string ComputeMd5(Stream stream)
    {
        var hash = MD5.HashData(stream);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Returns S3-style XML error response bodies so AWS SDKs can parse them correctly.
    /// </summary>
    private static string S3ErrorXml(string code, string message, string resource) =>
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <Error>
            <Code>{code}</Code>
            <Message>{message}</Message>
            <Resource>{resource}</Resource>
        </Error>
        """;

    private static string GetContentType(string key) =>
        string.IsNullOrWhiteSpace(key)
            ? "application/octet-stream"
            : ContentTypeProvider.TryGetContentType(key, out var contentType)
                ? contentType ?? "application/octet-stream"
                : "application/octet-stream";
}
