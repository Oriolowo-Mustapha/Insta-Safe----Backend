using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Delivery.Queries.GetDeliverySessionStatus;

public class GetDeliverySessionStatusQueryHandler : IRequestHandler<GetDeliverySessionStatusQuery, Result<DeliverySessionStatusResponse>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetDeliverySessionStatusQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<DeliverySessionStatusResponse>> Handle(GetDeliverySessionStatusQuery request, CancellationToken cancellationToken)
    {
        var session = await _unitOfWork.DeliverySessions.GetBySessionIdAsync(request.SessionId, cancellationToken);

        if (session == null)
            return Result<DeliverySessionStatusResponse>.Failure("Delivery session not found.");

        var response = new DeliverySessionStatusResponse
        {
            SessionId = session.SessionId,
            OrderId = session.OrderId,
            Status = session.Status.ToString(),
            PickupTimestamp = session.PickupTimestamp,
            DeliveryTimestamp = session.DeliveryTimestamp,
            ExpiresAt = session.ExpiresAt,
            FailureReason = session.FailureReason?.ToString()
        };

        return Result<DeliverySessionStatusResponse>.Success(response);
    }
}
