using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Events;
using MediatR;

namespace InstaSafe.Application.Orders.Commands.CreateOrder;

public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Result<CreateOrderResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateOrderCommandHandler(IUnitOfWork unitOfWork, IDateTimeProvider dateTimeProvider)
    {
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<CreateOrderResponse>> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var merchantExists = await _unitOfWork.Orders.MerchantExistsAsync(request.MerchantId, cancellationToken);

        if (!merchantExists)
            return Result<CreateOrderResponse>.Failure("Merchant not found.");

        var orderReference = await GenerateOrderReference(cancellationToken);

        var order = new Order
        {
            OrderReference = orderReference,
            MerchantId = request.MerchantId,
            ItemName = request.ItemName,
            ItemDescription = request.ItemDescription,
            ItemImageUrl = request.ItemImageUrl,
            Price = request.Price,
            DeliveryAddress = request.DeliveryAddress
        };

        order.AddDomainEvent(new OrderCreatedEvent(order.Id));

        _unitOfWork.Orders.Add(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CreateOrderResponse>.Success(new CreateOrderResponse(order.Id, order.OrderReference));
    }

    private async Task<string> GenerateOrderReference(CancellationToken cancellationToken)
    {
        var today = _dateTimeProvider.UtcNow.ToString("yyyyMMdd");
        var prefix = $"INSTA-ORD-{today}";
        var count = await _unitOfWork.Orders.CountOrdersByReferencePrefixAsync(prefix, cancellationToken);

        return $"{prefix}-{(count + 1):D4}";
    }
}
