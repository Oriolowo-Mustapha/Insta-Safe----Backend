namespace InstaSafe.Application.Common.Models.Monnify;

public class MonnifyOptions
{
    public const string SectionName = "Monnify";
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string ContractCode { get; set; } = string.Empty;
    public string WalletAccountNumber { get; set; } = string.Empty;
}
