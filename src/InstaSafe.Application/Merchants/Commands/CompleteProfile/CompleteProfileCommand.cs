using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Merchants.Commands.CompleteProfile;

public record CompleteProfileCommand(
    Guid MerchantUserId,
    string Bvn,
    string? Nin,
    string PayoutBankAccount,
    string PayoutBankCode) : IRequest<Result<string>>;
