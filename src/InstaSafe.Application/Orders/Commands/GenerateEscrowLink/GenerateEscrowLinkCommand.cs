using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Orders.Commands.GenerateEscrowLink;

public record GenerateEscrowLinkCommand(
    Guid OrderId,
    string BuyerFirstName,
    string BuyerLastName,
    string BuyerEmail,
    string BuyerPhone
) : IRequest<Result<EscrowLinkResponse>>;

public record EscrowLinkResponse(
    Guid OrderId,
    string VirtualAccountNumber,
    string VirtualBankCode,
    DateTime ExpiresAt
);
