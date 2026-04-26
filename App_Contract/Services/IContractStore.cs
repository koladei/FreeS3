using App_Contract.Contracts;

namespace App_Contract.Services;

public interface IContractStore
{
    ContractTemplateDto CreateTemplate(CreateTemplateFromS3Request request);
    ContractTemplateDto? GetTemplate(string templateId);
    IReadOnlyCollection<PlaceholderDto>? GetPlaceholders(string templateId);
    PlaceholderDto? AddPlaceholder(string templateId, AddPlaceholderRequest request);
    PlaceholderDto? UpdatePlaceholder(string templateId, string placeholderId, UpdatePlaceholderRequest request);
    bool? DeletePlaceholder(string templateId, string placeholderId);
    ContractInstanceDto? CreateInstance(CreateContractInstanceRequest request);
    ContractInstanceDto? GetInstance(string instanceId);
    FieldValueDto? SubmitFieldValue(string instanceId, SubmitFieldValueRequest request);
    ContractInstanceDto? FinalizeInstance(string instanceId, FinalizeContractRequest request);
}
