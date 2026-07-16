using System.Net.Http.Json;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models.AlatPay;

namespace InstaSafe.Infrastructure.ExternalServices.AlatPay;

public class AlatPayClient : IAlatPayClient
{
    private readonly HttpClient _httpClient;

    public AlatPayClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<VirtualAccountResponse> GenerateVirtualAccountAsync(VirtualAccountRequest request, CancellationToken ct)
    {
        var response = await _httpClient.PostAsJsonAsync("/bank-transfer/api/v1/bankTransfer/virtualAccount", request, ct);
        
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            throw new Exception($"ALATPay API Error: {response.StatusCode} - {content}");
        }

        var result = await response.Content.ReadFromJsonAsync<VirtualAccountResponse>(cancellationToken: ct);
        return result ?? throw new Exception("Failed to deserialize ALATPay response.");
    }

    public async Task<CardPaymentResponse> InitiateCardPaymentAsync(CardPaymentRequest request, CancellationToken ct)
    {
        var response = await _httpClient.PostAsJsonAsync("/card-payments/api/v1/card/initialize", request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            throw new Exception($"ALATPay API Error: {response.StatusCode} - {content}");
        }

        var result = await response.Content.ReadFromJsonAsync<CardPaymentResponse>(cancellationToken: ct);
        return result ?? throw new Exception("Failed to deserialize ALATPay response.");
    }

    public async Task<TransactionStatusResponse> CheckTransactionStatusAsync(string channelId, string transactionReference, CancellationToken ct)
    {
        var response = await _httpClient.GetAsync($"/pay-with-bank-account/api/EcommerceTransfer/CheckTransactionStatus/{channelId}/{transactionReference}", ct);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            throw new Exception($"ALATPay API Error: {response.StatusCode} - {content}");
        }

        var result = await response.Content.ReadFromJsonAsync<TransactionStatusResponse>(cancellationToken: ct);
        return result ?? throw new Exception("Failed to deserialize ALATPay response.");
    }
}
