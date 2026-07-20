using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Common.Models.Monnify;
using MediatR;

namespace InstaSafe.Application.Merchants.Queries.GetBanks;

public record GetBanksQuery() : IRequest<Result<List<BankResponse>>>;
