namespace InstaSafe.Application.Common.Models.Monnify;

public record BankResponse(string Name, string Code, string? UssdTemplate, string? BaseUssdCode, string? TransferUssdTemplate);
