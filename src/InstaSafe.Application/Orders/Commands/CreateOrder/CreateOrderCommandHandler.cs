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
    private readonly IFraudScoringEngine _fraudScoringEngine;

    public CreateOrderCommandHandler(IUnitOfWork unitOfWork, IDateTimeProvider dateTimeProvider, IFraudScoringEngine fraudScoringEngine)
    {
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _fraudScoringEngine = fraudScoringEngine;
    }

    public async Task<Result<CreateOrderResponse>> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var merchants = await _unitOfWork.Repository<Merchant>().FindAsync(m => m.UserId == request.MerchantId, cancellationToken);
        var merchant = merchants.FirstOrDefault();

        if (merchant == null)
        {
            // Fallback in case request.MerchantId was actually the Merchant.Id
            merchant = await _unitOfWork.Repository<Merchant>().GetByIdAsync(request.MerchantId, cancellationToken);
            if (merchant == null)
                return Result<CreateOrderResponse>.Failure("Merchant not found.");
        }

        if (!merchant.IsVerified)
        {
            return Result<CreateOrderResponse>.Failure("Profile incomplete. Please update your KYC and bank details to create orders.");
        }

        // 1.5 & 1.6: Build Fraud Scoring Engine & Apply Risk Actions
        var riskResult = await _fraudScoringEngine.EvaluateMerchantRiskAsync(merchant, cancellationToken);
        
        if (riskResult.RiskLevel == "High")
        {
            return Result<CreateOrderResponse>.Failure($"Transaction blocked due to high fraud risk. Score: {riskResult.Score}. Reason: {string.Join(", ", riskResult.Factors)}");
        }

        var orderReference = await GenerateOrderReference(cancellationToken);

        var order = new Order
        {
            OrderReference = orderReference,
            MerchantId = merchant.Id,
            ItemName = request.ItemName,
            ItemDescription = request.ItemDescription,
            ItemImageUrl = request.ItemImageUrl,
            Price = request.Price,
            DeliveryAddress = request.DeliveryAddress,
            RiskScore = riskResult.Score,
            RiskLevel = riskResult.RiskLevel
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
