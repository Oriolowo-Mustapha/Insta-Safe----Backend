using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Application.Orders.Queries.GetOrderById;

public class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, Result<OrderDetailResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetOrderByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<OrderDetailResponse>> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Merchant)
            .Include(o => o.Buyer)
            .Include(o => o.EscrowTransaction)
            .Include(o => o.DeliverySession)
            .Include(o => o.Dispute)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order == null)
            return Result<OrderDetailResponse>.Failure("Order not found.");

        var response = new OrderDetailResponse
        {
            Id = order.Id,
            OrderReference = order.OrderReference,
            ItemName = order.ItemName,
            ItemDescription = order.ItemDescription,
            ItemImageUrl = order.ItemImageUrl,
            Price = order.Price,
            Currency = order.Currency,
            DeliveryAddress = order.DeliveryAddress,
            Status = order.Status.ToString(),
            EscrowLinkUrl = order.EscrowLinkUrl,
            FundedAt = order.FundedAt,
            DeliveredAt = order.DeliveredAt,
            ValidationWindowExpiresAt = order.ValidationWindowExpiresAt,
            CompletedAt = order.CompletedAt,
            CreatedAt = order.CreatedAt,
            Merchant = order.Merchant != null
                ? new MerchantInfo { Id = order.Merchant.Id, BusinessName = order.Merchant.BusinessName }
                : null,
            Buyer = order.Buyer != null
                ? new BuyerInfo
                {
                    Id = order.Buyer.Id,
                    FirstName = order.Buyer.FirstName,
                    LastName = order.Buyer.LastName,
                    Email = order.Buyer.Email,
                    Phone = order.Buyer.Phone
                }
                : null,
            EscrowTransaction = order.EscrowTransaction != null
                ? new EscrowTransactionInfo
                {
                    MonnifyTransactionReference = order.EscrowTransaction.MonnifyTransactionReference,
                    Channel = order.EscrowTransaction.Channel.ToString(),
                    Amount = order.EscrowTransaction.Amount,
                    Status = order.EscrowTransaction.Status.ToString(),
                    VirtualAccountNumber = order.EscrowTransaction.VirtualAccountNumber,
                    VirtualBankCode = order.EscrowTransaction.VirtualBankCode,
                    FundedAt = order.EscrowTransaction.FundedAt
                }
                : null,
            DeliverySession = order.DeliverySession != null
                ? new DeliverySessionInfo
                {
                    SessionId = order.DeliverySession.SessionId,
                    Status = order.DeliverySession.Status.ToString(),
                    PickupTimestamp = order.DeliverySession.PickupTimestamp,
                    DeliveryTimestamp = order.DeliverySession.DeliveryTimestamp
                }
                : null,
            Dispute = order.Dispute != null
                ? new DisputeInfo
                {
                    Id = order.Dispute.Id,
                    Reason = order.Dispute.Reason,
                    Status = order.Dispute.Status.ToString(),
                    ResolvedAt = order.Dispute.ResolvedAt
                }
                : null
        };

        return Result<OrderDetailResponse>.Success(response);
    }
}
