using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Domain.Enums;
using MediatR;

namespace InstaSafe.Application.Orders.Queries.GetMerchantOrders;

public class GetMerchantOrdersQueryHandler : IRequestHandler<GetMerchantOrdersQuery, Result<PaginatedList<MerchantOrderResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetMerchantOrdersQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PaginatedList<MerchantOrderResponse>>> Handle(GetMerchantOrdersQuery request, CancellationToken cancellationToken)
    {
        OrderStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(request.StatusFilter) && Enum.TryParse<OrderStatus>(request.StatusFilter, ignoreCase: true, out var parsedStatus))
        {
            statusFilter = parsedStatus;
        }

        var (items, totalCount) = await _unitOfWork.Orders.GetMerchantOrdersAsync(
            request.MerchantId, request.PageNumber, request.PageSize, statusFilter, cancellationToken);

        var responseItems = items.Select(o => new MerchantOrderResponse
        {
            Id = o.Id,
            OrderReference = o.OrderReference,
            ItemName = o.ItemName,
            Price = o.Price,
            Status = o.Status.ToString(),
            CreatedAt = o.CreatedAt,
            BuyerName = o.Buyer != null ? $"{o.Buyer.FirstName} {o.Buyer.LastName}" : null
        }).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);
        var result = new PaginatedList<MerchantOrderResponse>(responseItems, totalCount, request.PageNumber, totalPages);

        return Result<PaginatedList<MerchantOrderResponse>>.Success(result);
    }
}
