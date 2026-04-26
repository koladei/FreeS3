namespace App_Contract.Contracts;

public record CreateTemplateFromS3Request(
    string Bucket,
    string ObjectKey,
    string FileName,
    string? ContentType,
    string? Title,
    string? AuthorId
);

public record AddPlaceholderRequest(
    string FieldType,
    string Role,
    int Page,
    decimal X,
    decimal Y,
    decimal Width,
    decimal Height,
    bool Required,
    string? Label,
    int? MaxLength,
    int? Order
);

public record UpdatePlaceholderRequest(
    string FieldType,
    string Role,
    int Page,
    decimal X,
    decimal Y,
    decimal Width,
    decimal Height,
    bool Required,
    string? Label,
    int? MaxLength,
    int? Order
);

public record CreateContractInstanceRequest(
    string TemplateId,
    List<SignerDto> Signers,
    string? Name
);

public record SignerDto(
    string SignerId,
    string Role,
    string DisplayName,
    string? Email,
    int RoutingOrder
);

public record SubmitFieldValueRequest(
    string SignerId,
    string PlaceholderId,
    string? Value,
    string? SignatureData,
    string? SourceIp
);

public record FinalizeContractRequest(
    string FinalizedBy,
    bool ApplyDigitalSignature,
    string? SignatureProfile,
    string? TimestampAuthorityUrl
);

public record ContractTemplateDto(
    string TemplateId,
    string Bucket,
    string ObjectKey,
    string FileName,
    string? ContentType,
    string? Title,
    string? AuthorId,
    DateTimeOffset CreatedAt,
    string Status,
    IReadOnlyCollection<PlaceholderDto> Placeholders
);

public record PlaceholderDto(
    string PlaceholderId,
    string FieldType,
    string Role,
    int Page,
    decimal X,
    decimal Y,
    decimal Width,
    decimal Height,
    bool Required,
    string? Label,
    int? MaxLength,
    int? Order
);

public record ContractInstanceDto(
    string InstanceId,
    string TemplateId,
    string? Name,
    DateTimeOffset CreatedAt,
    string Status,
    bool ReadyForFinalization,
    bool IsDigitallySigned,
    IReadOnlyCollection<SignerDto> Signers,
    IReadOnlyCollection<FieldValueDto> FieldValues,
    string? FinalArtifactHash,
    DateTimeOffset? SignedAt
);

public record FieldValueDto(
    string PlaceholderId,
    string SignerId,
    string? Value,
    string? SignatureData,
    DateTimeOffset SubmittedAt,
    string? SourceIp
);
