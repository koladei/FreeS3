using System.Security.Cryptography;
using System.Text;
using App_Contract.Contracts;
using CradleSoft.DMS.Data;
using CradleSoft.DMS.Models;
using Microsoft.EntityFrameworkCore;

namespace App_Contract.Services;

public sealed class DbContractStore : IContractStore
{
    private readonly AppDbContext _db;

    public DbContractStore(AppDbContext db)
    {
        _db = db;
    }

    public ContractTemplateDto CreateTemplate(CreateTemplateFromS3Request request)
    {
        var template = new ContractTemplate
        {
            TemplateId = Guid.NewGuid().ToString("N"),
            Bucket = request.Bucket,
            ObjectKey = request.ObjectKey,
            FileName = request.FileName,
            ContentType = request.ContentType,
            Title = request.Title,
            AuthorId = request.AuthorId,
            CreatedAt = DateTimeOffset.UtcNow,
            Status = "Draft"
        };

        _db.ContractTemplates.Add(template);
        _db.SaveChanges();

        return ToTemplateDto(template, []);
    }

    public ContractTemplateDto? GetTemplate(string templateId)
    {
        var template = _db.ContractTemplates
            .AsNoTracking()
            .Include(x => x.Placeholders)
            .FirstOrDefault(x => x.TemplateId == templateId);

        if (template is null)
        {
            return null;
        }

        var placeholders = template.Placeholders
            .OrderBy(x => x.Order ?? int.MaxValue)
            .ThenBy(x => x.Page)
            .ThenBy(x => x.Y)
            .ThenBy(x => x.X)
            .ToList();

        return ToTemplateDto(template, placeholders);
    }

    public IReadOnlyCollection<PlaceholderDto>? GetPlaceholders(string templateId)
    {
        var exists = _db.ContractTemplates
            .AsNoTracking()
            .Any(x => x.TemplateId == templateId);

        if (!exists)
        {
            return null;
        }

        return _db.ContractPlaceholders
            .AsNoTracking()
            .Where(x => x.TemplateId == templateId)
            .OrderBy(x => x.Order ?? int.MaxValue)
            .ThenBy(x => x.Page)
            .ThenBy(x => x.Y)
            .ThenBy(x => x.X)
            .Select(ToPlaceholderDto)
            .ToList();
    }

    public PlaceholderDto? AddPlaceholder(string templateId, AddPlaceholderRequest request)
    {
        var exists = _db.ContractTemplates.Any(x => x.TemplateId == templateId);
        if (!exists)
        {
            return null;
        }

        var placeholder = new ContractPlaceholder
        {
            PlaceholderId = Guid.NewGuid().ToString("N"),
            TemplateId = templateId,
            FieldType = request.FieldType,
            Role = request.Role,
            Page = request.Page,
            X = request.X,
            Y = request.Y,
            Width = request.Width,
            Height = request.Height,
            Required = request.Required,
            Label = request.Label,
            MaxLength = request.MaxLength,
            Order = request.Order
        };

        _db.ContractPlaceholders.Add(placeholder);
        _db.SaveChanges();

        return ToPlaceholderDto(placeholder);
    }

    public PlaceholderDto? UpdatePlaceholder(string templateId, string placeholderId, UpdatePlaceholderRequest request)
    {
        var placeholder = _db.ContractPlaceholders
            .FirstOrDefault(x => x.TemplateId == templateId && x.PlaceholderId == placeholderId);

        if (placeholder is null)
        {
            return null;
        }

        placeholder.FieldType = request.FieldType;
        placeholder.Role = request.Role;
        placeholder.Page = request.Page;
        placeholder.X = request.X;
        placeholder.Y = request.Y;
        placeholder.Width = request.Width;
        placeholder.Height = request.Height;
        placeholder.Required = request.Required;
        placeholder.Label = request.Label;
        placeholder.MaxLength = request.MaxLength;
        placeholder.Order = request.Order;

        _db.SaveChanges();

        return ToPlaceholderDto(placeholder);
    }

    public bool? DeletePlaceholder(string templateId, string placeholderId)
    {
        var templateExists = _db.ContractTemplates.Any(x => x.TemplateId == templateId);
        if (!templateExists)
        {
            return null;
        }

        var placeholder = _db.ContractPlaceholders
            .FirstOrDefault(x => x.TemplateId == templateId && x.PlaceholderId == placeholderId);

        if (placeholder is null)
        {
            return false;
        }

        var instanceIds = _db.ContractInstances
            .Where(x => x.TemplateId == templateId)
            .Select(x => x.InstanceId)
            .ToList();

        if (instanceIds.Count > 0)
        {
            var relatedValues = _db.ContractFieldValues
                .Where(x => x.PlaceholderId == placeholderId && instanceIds.Contains(x.InstanceId));

            _db.ContractFieldValues.RemoveRange(relatedValues);
        }

        _db.ContractPlaceholders.Remove(placeholder);
        _db.SaveChanges();

        return true;
    }

    public ContractInstanceDto? CreateInstance(CreateContractInstanceRequest request)
    {
        var template = _db.ContractTemplates
            .Include(x => x.Placeholders)
            .FirstOrDefault(x => x.TemplateId == request.TemplateId);

        if (template is null)
        {
            return null;
        }

        var instance = new ContractInstance
        {
            InstanceId = Guid.NewGuid().ToString("N"),
            TemplateId = request.TemplateId,
            Name = request.Name,
            CreatedAt = DateTimeOffset.UtcNow,
            Status = "InProgress",
            IsDigitallySigned = false
        };

        foreach (var signer in request.Signers)
        {
            instance.Signers.Add(new ContractSigner
            {
                SignerId = signer.SignerId,
                Role = signer.Role,
                DisplayName = signer.DisplayName,
                Email = signer.Email,
                RoutingOrder = signer.RoutingOrder,
            });
        }

        template.Status = "InUse";
        _db.ContractInstances.Add(instance);
        _db.SaveChanges();

        return ToInstanceDto(instance, template.Placeholders.ToList());
    }

    public ContractInstanceDto? GetInstance(string instanceId)
    {
        var instance = _db.ContractInstances
            .AsNoTracking()
            .Include(x => x.Signers)
            .Include(x => x.FieldValues)
            .Include(x => x.Template)
            .ThenInclude(x => x.Placeholders)
            .FirstOrDefault(x => x.InstanceId == instanceId);

        if (instance is null)
        {
            return null;
        }

        return ToInstanceDto(instance, instance.Template.Placeholders.ToList());
    }

    public FieldValueDto? SubmitFieldValue(string instanceId, SubmitFieldValueRequest request)
    {
        var instance = _db.ContractInstances
            .Include(x => x.Signers)
            .Include(x => x.FieldValues)
            .Include(x => x.Template)
            .ThenInclude(x => x.Placeholders)
            .FirstOrDefault(x => x.InstanceId == instanceId);

        if (instance is null)
        {
            return null;
        }

        var placeholder = instance.Template.Placeholders
            .FirstOrDefault(x => x.PlaceholderId == request.PlaceholderId);

        if (placeholder is null)
        {
            return null;
        }

        var signer = instance.Signers
            .FirstOrDefault(x => x.SignerId == request.SignerId);

        if (signer is null)
        {
            return null;
        }

        if (!string.Equals(placeholder.Role, signer.Role, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var existing = instance.FieldValues
            .FirstOrDefault(x => x.PlaceholderId == request.PlaceholderId && x.SignerId == request.SignerId);

        if (existing is null)
        {
            existing = new ContractFieldValue
            {
                InstanceId = instanceId,
                PlaceholderId = request.PlaceholderId,
                SignerId = request.SignerId,
            };
            _db.ContractFieldValues.Add(existing);
        }

        existing.Value = request.Value;
        existing.SignatureData = request.SignatureData;
        existing.SubmittedAt = DateTimeOffset.UtcNow;
        existing.SourceIp = request.SourceIp;

        _db.SaveChanges();

        return ToFieldValueDto(existing);
    }

    public ContractInstanceDto? FinalizeInstance(string instanceId, FinalizeContractRequest request)
    {
        var instance = _db.ContractInstances
            .Include(x => x.Signers)
            .Include(x => x.FieldValues)
            .Include(x => x.Template)
            .ThenInclude(x => x.Placeholders)
            .FirstOrDefault(x => x.InstanceId == instanceId);

        if (instance is null)
        {
            return null;
        }

        if (!AllRequiredSatisfied(instance.Template.Placeholders, instance.FieldValues))
        {
            return null;
        }

        instance.Status = "Finalized";
        if (request.ApplyDigitalSignature)
        {
            instance.IsDigitallySigned = true;
            instance.SignedAt = DateTimeOffset.UtcNow;
            instance.FinalArtifactHash = ComputeFinalArtifactHash(instance, request.FinalizedBy, request.SignatureProfile);
        }

        _db.SaveChanges();

        return ToInstanceDto(instance, instance.Template.Placeholders.ToList());
    }

    private static bool AllRequiredSatisfied(IEnumerable<ContractPlaceholder> placeholders, IEnumerable<ContractFieldValue> fieldValues)
    {
        var values = fieldValues.ToList();
        return placeholders.Where(x => x.Required).All(required =>
            values.Any(value =>
                string.Equals(value.PlaceholderId, required.PlaceholderId, StringComparison.OrdinalIgnoreCase)
                && (!string.IsNullOrWhiteSpace(value.Value) || !string.IsNullOrWhiteSpace(value.SignatureData))));
    }

    private static ContractTemplateDto ToTemplateDto(ContractTemplate template, IReadOnlyCollection<ContractPlaceholder> placeholders)
    {
        return new ContractTemplateDto(
            TemplateId: template.TemplateId,
            Bucket: template.Bucket,
            ObjectKey: template.ObjectKey,
            FileName: template.FileName,
            ContentType: template.ContentType,
            Title: template.Title,
            AuthorId: template.AuthorId,
            CreatedAt: template.CreatedAt,
            Status: template.Status,
            Placeholders: placeholders.Select(ToPlaceholderDto).ToList()
        );
    }

    private static PlaceholderDto ToPlaceholderDto(ContractPlaceholder placeholder)
    {
        return new PlaceholderDto(
            PlaceholderId: placeholder.PlaceholderId,
            FieldType: placeholder.FieldType,
            Role: placeholder.Role,
            Page: placeholder.Page,
            X: placeholder.X,
            Y: placeholder.Y,
            Width: placeholder.Width,
            Height: placeholder.Height,
            Required: placeholder.Required,
            Label: placeholder.Label,
            MaxLength: placeholder.MaxLength,
            Order: placeholder.Order
        );
    }

    private static ContractInstanceDto ToInstanceDto(ContractInstance instance, IReadOnlyCollection<ContractPlaceholder> placeholders)
    {
        return new ContractInstanceDto(
            InstanceId: instance.InstanceId,
            TemplateId: instance.TemplateId,
            Name: instance.Name,
            CreatedAt: instance.CreatedAt,
            Status: instance.Status,
            ReadyForFinalization: AllRequiredSatisfied(placeholders, instance.FieldValues),
            IsDigitallySigned: instance.IsDigitallySigned,
            Signers: instance.Signers
                .OrderBy(x => x.RoutingOrder)
                .Select(x => new SignerDto(x.SignerId, x.Role, x.DisplayName, x.Email, x.RoutingOrder))
                .ToList(),
            FieldValues: instance.FieldValues
                .OrderBy(x => x.SubmittedAt)
                .Select(ToFieldValueDto)
                .ToList(),
            FinalArtifactHash: instance.FinalArtifactHash,
            SignedAt: instance.SignedAt
        );
    }

    private static FieldValueDto ToFieldValueDto(ContractFieldValue value)
    {
        return new FieldValueDto(
            PlaceholderId: value.PlaceholderId,
            SignerId: value.SignerId,
            Value: value.Value,
            SignatureData: value.SignatureData,
            SubmittedAt: value.SubmittedAt,
            SourceIp: value.SourceIp
        );
    }

    private static string ComputeFinalArtifactHash(ContractInstance instance, string finalizedBy, string? signatureProfile)
    {
        var payload = $"{instance.InstanceId}|{instance.TemplateId}|{finalizedBy}|{signatureProfile}|{DateTimeOffset.UtcNow:O}";
        var bytes = Encoding.UTF8.GetBytes(payload);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
