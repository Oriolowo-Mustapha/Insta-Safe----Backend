using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Orders.Queries.VerifyTransactionStatus;

public record VerifyTransactionStatusQuery(Guid OrderId) : IRequest<Result<TransactionVerificationResponse>>;

public class TransactionVerificationResponse
{
    public string Status { get; init; } = string.Empty;
    public bool IsFunded { get; init; }
    public string? AlatPayTransactionId { get; init; }
    public string? Detail { get; init; }
}
