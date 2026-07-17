using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Orders.Queries.GetOrderById;

public record GetOrderByIdQuery(Guid OrderId) : IRequest<Result<OrderDetailResponse>>;

public class OrderDetailResponse
{
    public Guid Id { get; init; }
    public string OrderReference { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public string? ItemDescription { get; init; }
    public string? ItemImageUrl { get; init; }
    public decimal Price { get; init; }
    public string Currency { get; init; } = "NGN";
    public string? DeliveryAddress { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? EscrowLinkUrl { get; init; }
    public DateTime? FundedAt { get; init; }
    public DateTime? DeliveredAt { get; init; }
    public DateTime? ValidationWindowExpiresAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime CreatedAt { get; init; }

    public MerchantInfo? Merchant { get; init; }
    public BuyerInfo? Buyer { get; init; }
    public EscrowTransactionInfo? EscrowTransaction { get; init; }
    public DeliverySessionInfo? DeliverySession { get; init; }
    public DisputeInfo? Dispute { get; init; }
}

public class MerchantInfo
{
    public Guid Id { get; init; }
    public string BusinessName { get; init; } = string.Empty;
}

public class BuyerInfo
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
}

public class EscrowTransactionInfo
{
    public string? MonnifyTransactionReference { get; init; }
    public string Channel { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? VirtualAccountNumber { get; init; }
    public string? VirtualBankCode { get; init; }
    public DateTime? FundedAt { get; init; }
}

public class DeliverySessionInfo
{
    public Guid SessionId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime? PickupTimestamp { get; init; }
    public DateTime? DeliveryTimestamp { get; init; }
}

public class DisputeInfo
{
    public Guid Id { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime? ResolvedAt { get; init; }
}
