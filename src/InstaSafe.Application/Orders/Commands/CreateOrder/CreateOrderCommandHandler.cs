using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Common.Models.Monnify;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Configuration;

namespace InstaSafe.Application.Orders.Commands.CreateOrder;

public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Result<CreateOrderResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IFraudScoringEngine _fraudScoringEngine;
    private readonly IMonnifyPaymentService _monnifyClient;
    private readonly InstaSafe.Application.Common.Models.Monnify.MonnifyOptions _options;
    private readonly IConfiguration _configuration;

    public CreateOrderCommandHandler(
        IApplicationDbContext context, 
        IUnitOfWork unitOfWork, 
        IDateTimeProvider dateTimeProvider, 
        IFraudScoringEngine fraudScoringEngine,
        IMonnifyPaymentService monnifyClient,
        Microsoft.Extensions.Options.IOptions<InstaSafe.Application.Common.Models.Monnify.MonnifyOptions> options,
        IConfiguration configuration)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _fraudScoringEngine = fraudScoringEngine;
        _monnifyClient = monnifyClient;
        _options = options.Value;
        _configuration = configuration;
    }

    public async Task<Result<CreateOrderResponse>> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var merchant = await _context.Merchants
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.UserId == request.MerchantId, cancellationToken);

        if (merchant == null)
        {
            merchant = await _context.Merchants
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.Id == request.MerchantId, cancellationToken);
                
            if (merchant == null)
                return Result<CreateOrderResponse>.Failure("Merchant not found.");
        }

        if (!merchant.IsVerified)
        {
            return Result<CreateOrderResponse>.Failure("Profile incomplete. Please update your KYC and bank details to create orders.");
        }

        var riskResult = await _fraudScoringEngine.EvaluateMerchantRiskAsync(merchant, cancellationToken);
        
        if (riskResult.RiskLevel == "High")
        {
            return Result<CreateOrderResponse>.Failure($"Transaction blocked due to high fraud risk. Score: {riskResult.Score}. Reason: {string.Join(", ", riskResult.Factors)}");
        }

        var buyer = await _unitOfWork.Buyers.GetOrCreateAsync(
            request.BuyerEmail, request.BuyerFirstName, request.BuyerLastName, request.BuyerPhone, cancellationToken);

        var orderReference = await GenerateOrderReference(cancellationToken);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderReference = orderReference,
            MerchantId = merchant.Id,
            ItemName = request.ItemName,
            ItemDescription = request.ItemDescription,
            ItemImageUrl = request.ItemImageUrl,
            Price = request.Price,
            DeliveryAddress = request.DeliveryAddress,
            DispatcherPhone = request.DispatcherPhone,
            RiskScore = riskResult.Score,
            RiskLevel = riskResult.RiskLevel
        };
        
        order.SetBuyer(buyer.Id);

        // Use the primary Escrow Monnify Wallet (No sub-account needed)
        // Funds will sit in the main wallet until single transfer disbursement

        var frontendUrl = _configuration["FrontendUrl:Production"] ?? "https://instasafe.vercel.app";
        var initReq = new InstaSafe.Application.Common.Models.Monnify.InitTransactionRequest(
            order.Price,
            $"{request.BuyerFirstName} {request.BuyerLastName}",
            request.BuyerEmail,
            order.OrderReference,
            $"InstaSafe Escrow: {order.ItemName}",
            "NGN",
            _options.ContractCode,
            $"{frontendUrl}/order/{order.OrderReference}", 
            new[] { "CARD", "ACCOUNT_TRANSFER", "USSD" },
            null // No split config, funds go to the main wallet
        );

        MonnifyBaseResponse<InitTransactionResponse> initResponse;
        try
        {
            initResponse = await _monnifyClient.InitializeTransactionAsync(initReq, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result<CreateOrderResponse>.Failure($"Failed to generate payment link: {ex.Message}");
        }

        if (!initResponse.RequestSuccessful)
            return Result<CreateOrderResponse>.Failure($"Monnify validation failed: {initResponse.ResponseMessage}");

        if (initResponse.ResponseBody == null || string.IsNullOrEmpty(initResponse.ResponseBody.CheckoutUrl))
            return Result<CreateOrderResponse>.Failure("Monnify did not return a valid checkout URL.");

        var escrowTransaction = new EscrowTransaction
        {
            OrderId = order.Id,
            MonnifyTransactionReference = initResponse.ResponseBody.TransactionReference,
            CheckoutUrl = initResponse.ResponseBody.CheckoutUrl,
            TransactionReference = order.OrderReference,
            Channel = InstaSafe.Domain.Enums.PaymentChannel.Card,
            Amount = order.Price
        };

        order.GenerateEscrowLink(initResponse.ResponseBody.CheckoutUrl, _dateTimeProvider.UtcNow.AddMinutes(60));
        order.AddDomainEvent(new OrderCreatedEvent(order.Id));

        _unitOfWork.Orders.Add(order);
        _unitOfWork.EscrowTransactions.Add(escrowTransaction);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CreateOrderResponse>.Success(new CreateOrderResponse(order.Id, order.OrderReference, initResponse.ResponseBody.CheckoutUrl));
    }

    private async Task<string> GenerateOrderReference(CancellationToken cancellationToken)
    {
        var today = _dateTimeProvider.UtcNow.ToString("yyyyMMdd");
        var prefix = $"INSTA-ORD-{today}";
        var count = await _unitOfWork.Orders.CountOrdersByReferencePrefixAsync(prefix, cancellationToken);

        var randomSuffix = Guid.NewGuid().ToString("N").Substring(0, 5).ToUpper();
        return $"{prefix}-{(count + 1):D4}-{randomSuffix}";
    }
}
