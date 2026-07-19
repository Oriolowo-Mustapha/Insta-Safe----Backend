using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using InstaSafe.Domain.Events;
using MediatR;

namespace InstaSafe.Application.Disputes.Commands.RaiseDispute;

public class RaiseDisputeCommandHandler : IRequestHandler<RaiseDisputeCommand, Result<RaiseDisputeResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IBackgroundJobService _backgroundJobService;

    public RaiseDisputeCommandHandler(IUnitOfWork unitOfWork, IDateTimeProvider dateTimeProvider, IBackgroundJobService backgroundJobService)
    {
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _backgroundJobService = backgroundJobService;
    }

    public async Task<Result<RaiseDisputeResponse>> Handle(RaiseDisputeCommand request, CancellationToken cancellationToken)
    {
        var order = await _unitOfWork.Orders.GetByIdWithAllAsync(request.OrderId, cancellationToken);
        if (order == null)
            return Result<RaiseDisputeResponse>.Failure("Order not found.");

        if (order.Status != OrderStatus.Delivered)
            return Result<RaiseDisputeResponse>.Failure("Disputes can only be raised on delivered orders.");

        if (order.BuyerId != request.BuyerId)
            return Result<RaiseDisputeResponse>.Failure("Only the buyer can raise a dispute.");

        if (order.ValidationWindowExpiresAt.HasValue && order.ValidationWindowExpiresAt.Value < _dateTimeProvider.UtcNow)
            return Result<RaiseDisputeResponse>.Failure("The dispute window has expired. Funds will be released to the merchant.");

        if (order.Dispute != null)
            return Result<RaiseDisputeResponse>.Failure("A dispute has already been raised for this order.");

        var dispute = new Dispute
        {
            OrderId = request.OrderId,
            RaisedByBuyerId = request.BuyerId,
            Reason = request.Reason,
            EvidenceUrls = request.EvidenceUrls
        };

        _unitOfWork.Repository<Dispute>().Add(dispute);

        order.MarkAsDisputed(dispute.Id);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Queue AI Auto-Resolution Job if evidence is provided
        if (request.EvidenceUrls != null && request.EvidenceUrls.Any())
        {
            _backgroundJobService.Enqueue<IAutoDisputeResolverJob>(x => x.ProcessAsync(dispute.Id));
        }

        return Result<RaiseDisputeResponse>.Success(
            new RaiseDisputeResponse(dispute.Id, "Open", "Dispute raised successfully. Our team will review it."));
    }
}
