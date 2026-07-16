using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Orders.Queries.GetMerchantOrders;

public record GetMerchantOrdersQuery(
    Guid MerchantId,
    int PageNumber = 1,
    int PageSize = 10,
    string? StatusFilter = null
) : IRequest<Result<PaginatedList<MerchantOrderResponse>>>;

public class MerchantOrderResponse
{
    public Guid Id { get; init; }
    public string OrderReference { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public string? BuyerName { get; init; }
}
