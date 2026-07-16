using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Payouts.Commands.ExecuteSplitPayout;

public record ExecuteSplitPayoutCommand(Guid OrderId) : IRequest<Result<ExecuteSplitPayoutResponse>>;

public record ExecuteSplitPayoutResponse(
    decimal TotalAmount,
    decimal MerchantAmount,
    decimal PlatformCommission,
    string PayoutStatus
);
