namespace InstaSafe.Application.Common.Interfaces;

public interface IPaystackClient
{
    Task<(string Reference, string AuthUrl)> InitializeTransactionAsync(
        string email, long amountKobo, Guid orderId, CancellationToken ct);
    Task<bool> VerifyTransactionAsync(string reference, CancellationToken ct);
    Task<string?> CreateRecipientAsync(string accountNumber, string bankCode, string name, CancellationToken ct);
    Task<string?> InitiateTransferAsync(long amountKobo, string recipientCode, string reason, CancellationToken ct);
    Task<(bool Success, string? RefundReference, string? Error)> RefundTransactionAsync(string reference, CancellationToken ct);
    Task<(bool Success, string? CustomerCode, string? Error)> CreateCustomerAsync(
        string email, string firstName, string lastName, string phone, Guid orderId, CancellationToken ct);
    Task<(bool Success, string? AccountNumber, string? AccountName, string? Bank, string? Error)> AssignDedicatedAccountAsync(
        string customerCode, string? preferredBank, CancellationToken ct);
    Task<List<(string Name, string Slug, string Code)>> ListTransferBanksAsync(CancellationToken ct);
    Task<(bool Success, string? AccountName, string? Error)> ResolveAccountAsync(
        string accountNumber, string bankCode, CancellationToken ct);
}
