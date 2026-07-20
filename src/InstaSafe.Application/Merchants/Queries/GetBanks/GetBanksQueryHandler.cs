using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Common.Models.Monnify;
using MediatR;
using Microsoft.Extensions.Caching.Memory;

namespace InstaSafe.Application.Merchants.Queries.GetBanks;

public class GetBanksQueryHandler : IRequestHandler<GetBanksQuery, Result<List<BankResponse>>>
{
    private readonly IMonnifyPaymentService _monnifyClient;
    private readonly IMemoryCache _cache;
    private const string BanksCacheKey = "MonnifyBanksList";

    public GetBanksQueryHandler(IMonnifyPaymentService monnifyClient, IMemoryCache cache)
    {
        _monnifyClient = monnifyClient;
        _cache = cache;
    }

    public async Task<Result<List<BankResponse>>> Handle(GetBanksQuery request, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(BanksCacheKey, out List<BankResponse>? cachedBanks) && cachedBanks != null)
        {
            return Result<List<BankResponse>>.Success(cachedBanks);
        }

        try
        {
            var response = await _monnifyClient.GetBanksAsync(cancellationToken);

            if (response.RequestSuccessful && response.ResponseBody != null)
            {
                var banks = response.ResponseBody.OrderBy(b => b.Name).ToList();
                _cache.Set(BanksCacheKey, banks, TimeSpan.FromHours(24));
                return Result<List<BankResponse>>.Success(banks);
            }

            return Result<List<BankResponse>>.Failure($"Failed to fetch banks: {response.ResponseMessage}");
        }
        catch (Exception ex)
        {
            return Result<List<BankResponse>>.Failure($"Error fetching banks: {ex.Message}");
        }
    }
}
