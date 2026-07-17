using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models.Monnify;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace InstaSafe.Infrastructure.ExternalServices.Monnify;

public class MonnifyClient : IMonnifyPaymentService
{
    private readonly HttpClient _httpClient;
    private readonly MonnifyOptions _options;
    private readonly IMemoryCache _cache;
    private const string TokenCacheKey = "MonnifyAccessToken";

    public MonnifyClient(HttpClient httpClient, IOptions<MonnifyOptions> options, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _cache = cache;
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(TokenCacheKey, out string? cachedToken) && !string.IsNullOrEmpty(cachedToken))
        {
            return cachedToken;
        }

        var authString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ApiKey}:{_options.SecretKey}"));
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authString);

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(ct);
        var authResult = JsonSerializer.Deserialize<MonnifyBaseResponse<MonnifyAuthResponse>>(content, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        if (authResult?.RequestSuccessful != true || authResult.ResponseBody == null)
            throw new Exception("Failed to authenticate with Monnify");

        var token = authResult.ResponseBody.AccessToken;
        var expiresIn = authResult.ResponseBody.ExpiresIn;

        _cache.Set(TokenCacheKey, token, TimeSpan.FromSeconds(expiresIn - 60));

        return token;
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct);
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (body != null)
        {
            var json = JsonSerializer.Serialize(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return request;
    }

    private async Task<MonnifyBaseResponse<T>> SendAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        var response = await _httpClient.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        
        if (!response.IsSuccessStatusCode)
        {
            // Try to parse as base response to get error message
            try
            {
                var errorResult = JsonSerializer.Deserialize<MonnifyBaseResponse<T>>(content, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                if (errorResult != null) return errorResult;
            }
            catch { }
            
            throw new Exception($"Monnify API request failed with status {response.StatusCode}: {content}");
        }

        var result = JsonSerializer.Deserialize<MonnifyBaseResponse<T>>(content, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return result ?? throw new Exception("Failed to deserialize Monnify response");
    }

    public async Task<MonnifyBaseResponse<SubAccountResponse>> CreateSubAccountAsync(SubAccountRequest request, CancellationToken ct = default)
    {
        // The Create Sub Account API expects an array
        var req = await CreateRequestAsync(HttpMethod.Post, "/api/v1/sub-accounts", new[] { request }, ct);
        
        // Since it expects an array and returns an array in responseBody, we need a special deserializer here
        var response = await _httpClient.SendAsync(req, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        response.EnsureSuccessStatusCode();

        var result = JsonSerializer.Deserialize<MonnifyBaseResponse<List<SubAccountResponse>>>(content, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        
        if (result?.RequestSuccessful != true || result.ResponseBody == null || !result.ResponseBody.Any())
            throw new Exception("Failed to create Monnify sub-account: " + result?.ResponseMessage);

        return new MonnifyBaseResponse<SubAccountResponse>(
            result.RequestSuccessful, 
            result.ResponseMessage, 
            result.ResponseCode, 
            result.ResponseBody.First());
    }

    public async Task<MonnifyBaseResponse<InitTransactionResponse>> InitializeTransactionAsync(InitTransactionRequest request, CancellationToken ct = default)
    {
        var req = await CreateRequestAsync(HttpMethod.Post, "/api/v1/merchant/transactions/init-transaction", request, ct);
        return await SendAsync<InitTransactionResponse>(req, ct);
    }

    public async Task<MonnifyBaseResponse<SingleTransferResponse>> InitiateSingleTransferAsync(SingleTransferRequest request, CancellationToken ct = default)
    {
        var req = await CreateRequestAsync(HttpMethod.Post, "/api/v2/disbursements/single", request, ct);
        return await SendAsync<SingleTransferResponse>(req, ct);
    }

    public async Task<MonnifyBaseResponse<RefundResponse>> InitiateRefundAsync(RefundRequest request, CancellationToken ct = default)
    {
        var req = await CreateRequestAsync(HttpMethod.Post, "/api/v1/refunds/initiate-refund", request, ct);
        return await SendAsync<RefundResponse>(req, ct);
    }
}
