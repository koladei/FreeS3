using Amazon.S3;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using CradleSoft.DMS.Services;

namespace CradleSoft.DMS.API.Controllers;

/// <summary>
/// Convenience multipart-form controller for uploading/downloading files
/// via the standard browser/Postman form-data workflow.
/// This delegates to the same IStorageService as the DMS,
/// so files land in the same local disk store.
/// </summary>
[ApiController]
[Route("sch/[controller]")]
public class FilesController : ControllerBase
{
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();
    private readonly IStorageService _storageService;
    private readonly IConfiguration _configuration;

    public FilesController(IStorageService storageService, IConfiguration configuration)
    {
        _storageService = storageService;
        _configuration = configuration;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile file, [FromQuery] string? bucket = null)
    {
        if (file.Length == 0) return BadRequest("Empty file.");

        var bucketName = bucket ?? _configuration["S3Settings:BucketName"] ?? "default";
        using var stream = file.OpenReadStream();

        await _storageService.UploadObjectAsync(bucketName, file.FileName, stream, file.ContentType);

        return Ok(new { Message = "File uploaded successfully!", Bucket = bucketName, Key = file.FileName });
    }

    [HttpGet("download/{bucket}/{fileName}")]
    public async Task<IActionResult> Download(string bucket, string fileName)
    {
        try
        {
            var stream = await _storageService.GetObjectAsync(bucket, fileName);
            var contentType = GetContentType(fileName);

            // Return known previewable file types inline so browsers can render them.
            if (IsPreviewableContentType(contentType))
            {
                return File(stream, contentType, enableRangeProcessing: true);
            }

            return File(stream, contentType, fileName, enableRangeProcessing: true);
        }
        catch (FileNotFoundException)
        {
            return NotFound($"File '{fileName}' not found in bucket '{bucket}'.");
        }
    }

    private static string GetContentType(string fileName) =>
        string.IsNullOrWhiteSpace(fileName)
            ? "application/octet-stream"
            : ContentTypeProvider.TryGetContentType(fileName, out var contentType)
                ? contentType ?? "application/octet-stream"
                : "application/octet-stream";

    private static bool IsPreviewableContentType(string contentType) =>
        contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
        || contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
        || contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
        || contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
        || contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
        || contentType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
        || contentType.Equals("application/xml", StringComparison.OrdinalIgnoreCase)
        || contentType.Equals("application/javascript", StringComparison.OrdinalIgnoreCase);
}
