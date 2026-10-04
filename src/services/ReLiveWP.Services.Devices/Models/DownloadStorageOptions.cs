namespace ReLiveWP.Services.Devices.Models;

public class DownloadStorageOptions
{
    public const string SectionName = "Downloads:R2";

    public string? AccountId { get; set; }
    public string? Bucket { get; set; }
    public string? AccessKeyId { get; set; }
    public string? SecretAccessKey { get; set; }
    public TimeSpan LinkLifetime { get; set; } = TimeSpan.FromHours(6);

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AccountId)
        && !string.IsNullOrWhiteSpace(Bucket)
        && !string.IsNullOrWhiteSpace(AccessKeyId)
        && !string.IsNullOrWhiteSpace(SecretAccessKey);
}
