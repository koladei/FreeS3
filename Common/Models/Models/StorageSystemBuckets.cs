namespace CradleSoft.DMS.Models;

public static class StorageSystemBuckets
{
    public const string MyOutgoingContracts = "My Outgoing Contracts";
    public const string MyOutgoingContractsPrefix = "__system_my_outgoing_contracts__";
    public const string IncomingContracts = "Incoming Contracts";
    public const string IncomingContractsVirtualName = "__virtual_incoming_contracts__";
    public static readonly Guid IncomingContractsVirtualBucketId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static string GetMyOutgoingContractsInternalName(string userId) =>
        $"{MyOutgoingContractsPrefix}{userId}";

    public static bool IsMyOutgoingContracts(string? bucketName) =>
        !string.IsNullOrWhiteSpace(bucketName)
        && (string.Equals(bucketName.Trim(), MyOutgoingContracts, StringComparison.OrdinalIgnoreCase)
            || bucketName.Trim().StartsWith(MyOutgoingContractsPrefix, StringComparison.OrdinalIgnoreCase));

    public static bool IsIncomingContracts(string? bucketName) =>
        !string.IsNullOrWhiteSpace(bucketName)
        && (string.Equals(bucketName.Trim(), IncomingContracts, StringComparison.OrdinalIgnoreCase)
            || string.Equals(bucketName.Trim(), IncomingContractsVirtualName, StringComparison.OrdinalIgnoreCase));

    public static bool IsReservedBucketName(string? bucketName) =>
        IsMyOutgoingContracts(bucketName) || IsIncomingContracts(bucketName);

    public static string ToDisplayName(string? bucketName)
    {
        if (IsMyOutgoingContracts(bucketName))
        {
            return MyOutgoingContracts;
        }

        if (IsIncomingContracts(bucketName))
        {
            return IncomingContracts;
        }

        return bucketName?.Trim() ?? string.Empty;
    }
}
