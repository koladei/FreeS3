namespace CradleSoft.DMS.Models;

public static class StorageSystemBuckets
{
    public const string MyOutgoingContracts = "My Outgoing Contracts";
    public const string MyOutgoingContractsPrefix = "__system_my_outgoing_contracts__";

    public static string GetMyOutgoingContractsInternalName(string userId) =>
        $"{MyOutgoingContractsPrefix}{userId}";

    public static bool IsMyOutgoingContracts(string? bucketName) =>
        !string.IsNullOrWhiteSpace(bucketName)
        && (string.Equals(bucketName.Trim(), MyOutgoingContracts, StringComparison.OrdinalIgnoreCase)
            || bucketName.Trim().StartsWith(MyOutgoingContractsPrefix, StringComparison.OrdinalIgnoreCase));

    public static string ToDisplayName(string? bucketName)
    {
        if (IsMyOutgoingContracts(bucketName))
        {
            return MyOutgoingContracts;
        }

        return bucketName?.Trim() ?? string.Empty;
    }
}
