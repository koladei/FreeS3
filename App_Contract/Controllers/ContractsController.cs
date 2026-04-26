using App_Contract.Contracts;
using App_Contract.Services;
using Microsoft.AspNetCore.Mvc;

namespace App_Contract.Controllers;

[ApiController]
[Route("api/contracts")]
public sealed class ContractsController : ControllerBase
{
    private readonly IContractStore _store;

    public ContractsController(IContractStore store)
    {
        _store = store;
    }

    [HttpPost("templates")]
    public ActionResult<ContractTemplateDto> CreateTemplate([FromBody] CreateTemplateFromS3Request request)
    {
        if (string.IsNullOrWhiteSpace(request.Bucket) || string.IsNullOrWhiteSpace(request.ObjectKey) || string.IsNullOrWhiteSpace(request.FileName))
        {
            return BadRequest("Bucket, objectKey, and fileName are required.");
        }

        var isPdf = string.Equals(request.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase)
            || request.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            || request.ObjectKey.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

        if (!isPdf)
        {
            return BadRequest("Only PDF templates are supported by App_Contract.");
        }

        var template = _store.CreateTemplate(request);
        return CreatedAtAction(nameof(GetTemplate), new { templateId = template.TemplateId }, template);
    }

    [HttpGet("templates/{templateId}")]
    public ActionResult<ContractTemplateDto> GetTemplate(string templateId)
    {
        var template = _store.GetTemplate(templateId);
        if (template is null)
        {
            return NotFound();
        }

        return Ok(template);
    }

    [HttpGet("templates/{templateId}/placeholders")]
    public ActionResult<IReadOnlyCollection<PlaceholderDto>> GetPlaceholders(string templateId)
    {
        var placeholders = _store.GetPlaceholders(templateId);
        if (placeholders is null)
        {
            return NotFound();
        }

        return Ok(placeholders);
    }

    [HttpPost("templates/{templateId}/placeholders")]
    public ActionResult<PlaceholderDto> AddPlaceholder(string templateId, [FromBody] AddPlaceholderRequest request)
    {
        if (request.Page <= 0 || request.Width <= 0 || request.Height <= 0)
        {
            return BadRequest("Page must be greater than 0, and width/height must be positive.");
        }

        var placeholder = _store.AddPlaceholder(templateId, request);
        if (placeholder is null)
        {
            return NotFound();
        }

        return CreatedAtAction(nameof(GetPlaceholders), new { templateId }, placeholder);
    }

    [HttpPut("templates/{templateId}/placeholders/{placeholderId}")]
    public ActionResult<PlaceholderDto> UpdatePlaceholder(string templateId, string placeholderId, [FromBody] UpdatePlaceholderRequest request)
    {
        if (request.Page <= 0 || request.Width <= 0 || request.Height <= 0)
        {
            return BadRequest("Page must be greater than 0, and width/height must be positive.");
        }

        var placeholder = _store.UpdatePlaceholder(templateId, placeholderId, request);
        if (placeholder is null)
        {
            return NotFound();
        }

        return Ok(placeholder);
    }

    [HttpDelete("templates/{templateId}/placeholders/{placeholderId}")]
    public IActionResult DeletePlaceholder(string templateId, string placeholderId)
    {
        var removed = _store.DeletePlaceholder(templateId, placeholderId);
        if (removed is null)
        {
            return NotFound();
        }

        if (removed == false)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPost("instances")]
    public ActionResult<ContractInstanceDto> CreateInstance([FromBody] CreateContractInstanceRequest request)
    {
        if (request.Signers.Count == 0)
        {
            return BadRequest("At least one signer is required.");
        }

        var instance = _store.CreateInstance(request);
        if (instance is null)
        {
            return NotFound("Template not found.");
        }

        return CreatedAtAction(nameof(GetInstance), new { instanceId = instance.InstanceId }, instance);
    }

    [HttpGet("instances/{instanceId}")]
    public ActionResult<ContractInstanceDto> GetInstance(string instanceId)
    {
        var instance = _store.GetInstance(instanceId);
        if (instance is null)
        {
            return NotFound();
        }

        return Ok(instance);
    }

    [HttpPost("instances/{instanceId}/field-values")]
    public ActionResult<FieldValueDto> SubmitFieldValue(string instanceId, [FromBody] SubmitFieldValueRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PlaceholderId) || string.IsNullOrWhiteSpace(request.SignerId))
        {
            return BadRequest("placeholderId and signerId are required.");
        }

        var sourceIp = request.SourceIp ?? HttpContext.Connection.RemoteIpAddress?.ToString();
        var withIp = request with { SourceIp = sourceIp };

        var fieldValue = _store.SubmitFieldValue(instanceId, withIp);
        if (fieldValue is null)
        {
            return BadRequest("Field value submission failed. Validate instance, signer role, and placeholder IDs.");
        }

        return Ok(fieldValue);
    }

    [HttpPost("instances/{instanceId}/finalize")]
    public ActionResult<ContractInstanceDto> FinalizeInstance(string instanceId, [FromBody] FinalizeContractRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FinalizedBy))
        {
            return BadRequest("finalizedBy is required.");
        }

        var instance = _store.FinalizeInstance(instanceId, request);
        if (instance is null)
        {
            return BadRequest("Instance cannot be finalized until all required placeholders are completed.");
        }

        return Ok(instance);
    }
}
