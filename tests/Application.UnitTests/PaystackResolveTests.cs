using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Infrastructure.ExternalServices;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text;

namespace Application.UnitTests;

public class PaystackResolveTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _json;
        public StubHandler(HttpStatusCode status, string json)
        {
            _status = status; _json = json;
        }
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json")
            });
    }

    private sealed class FakeConfig : Microsoft.Extensions.Configuration.IConfiguration
    {
        public string? this[string key]
        {
            get => key == "Paystack:SecretKey" ? "sk_test_x" : null;
            set { }
        }
        public IEnumerable<Microsoft.Extensions.Configuration.IConfigurationSection> GetChildren() => [];
        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => throw new NotImplementedException();
        public Microsoft.Extensions.Configuration.IConfigurationSection GetSection(string key) => throw new NotImplementedException();
    }

    private static PaystackClient Client(HttpStatusCode status, string json) =>
        new(new HttpClient(new StubHandler(status, json)),
            new FakeConfig(), NullLogger<PaystackClient>.Instance);

    [Fact]
    public async Task Resolve_ReturnsAccountName()
    {
        var client = Client(HttpStatusCode.OK,
            """{"status":true,"data":{"account_number":"0123456789","account_name":"ADA OBI"}}""");

        var result = await client.ResolveAccountAsync("0123456789", "058", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("ADA OBI", result.AccountName);
    }

    [Fact]
    public async Task Resolve_BadAccount_ReturnsInvalidWithReason()
    {
        var client = Client(HttpStatusCode.BadRequest,
            """{"status":false,"message":"Invalid account number"}""");

        var result = await client.ResolveAccountAsync("000", "058", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ResolveFailureKind.Invalid, result.FailureKind);
        Assert.Contains("Invalid account number", result.Error);
        Assert.Contains("Check the number", result.Error);
    }

    [Fact]
    public async Task Resolve_RateLimit_ReturnsUnavailable()
    {
        var client = Client(HttpStatusCode.BadRequest,
            """{"status":false,"message":"Test mode daily limit of 3 live bank resolves exceeded. Use test bank codes 001."}""");

        var result = await client.ResolveAccountAsync("0123456789", "058", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ResolveFailureKind.Unavailable, result.FailureKind);
        Assert.Contains("temporarily unavailable", result.Error);
    }

    [Fact]
    public async Task Resolve_ServerError_ReturnsUnavailable()
    {
        var client = Client(HttpStatusCode.BadGateway, "oops");

        var result = await client.ResolveAccountAsync("0123456789", "058", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ResolveFailureKind.Unavailable, result.FailureKind);
    }

    [Fact]
    public async Task ListBanks_ParsesNameSlugCode()
    {
        var client = Client(HttpStatusCode.OK,
            """{"status":true,"data":[{"name":"Guaranty Trust Bank","slug":"guaranty-trust-bank","code":"058"},{"name":"Bad Entry","slug":"","code":""}]}""");

        var banks = await client.ListTransferBanksAsync(CancellationToken.None);

        Assert.Equal(2, banks.Count);
        Assert.Equal(("Guaranty Trust Bank", "guaranty-trust-bank", "058"), banks[0]);
    }

    [Fact]
    public async Task ListBanks_Failure_ReturnsEmpty()
    {
        var client = Client(HttpStatusCode.InternalServerError, "{}");

        var banks = await client.ListTransferBanksAsync(CancellationToken.None);

        Assert.Empty(banks);
    }

    private sealed class QueueHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode, string)> _responses;
        public int Calls { get; private set; }
        public string LastUrl { get; private set; } = "";
        public QueueHandler(IEnumerable<(HttpStatusCode, string)> responses)
            => _responses = new Queue<(HttpStatusCode, string)>(responses);
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            LastUrl = request.RequestUri?.PathAndQuery ?? "";
            var (status, json) = _responses.Dequeue();
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    private static (PaystackClient Client, QueueHandler Handler) Paged(
        params (HttpStatusCode, string)[] responses)
    {
        var handler = new QueueHandler(responses);
        return (new PaystackClient(new HttpClient(handler),
            new FakeConfig(), NullLogger<PaystackClient>.Instance), handler);
    }

    private static string BankList(params (string Name, string Slug, string Code)[] banks) =>
        """{"status":true,"data":[""" +
        string.Join(",", banks.Select(b =>
            $$"""{"name":"{{b.Name}}","slug":"{{b.Slug}}","code":"{{b.Code}}"}""")) +
        "]}";

    [Fact]
    public async Task ListAllBanks_ReturnsFullList_InOneCall()
    {
        var (client, handler) = Paged((HttpStatusCode.OK, BankList(
            ("Abbey Mortgage Bank", "abbey-mortgage-bank", "801"),
            ("Coronation Merchant Bank", "coronation-merchant-bank", "559"))));

        var banks = await client.ListAllBanksAsync(CancellationToken.None);

        Assert.Equal(2, banks.Count);
        Assert.Equal("801", banks[0].Code);
        Assert.Equal(1, handler.Calls);
        Assert.Contains("perPage=100", handler.LastUrl);
        Assert.DoesNotContain("use_cursor", handler.LastUrl);
    }

    [Fact]
    public async Task ListAllBanks_DedupesRepeatedCodes()
    {
        var (client, handler) = Paged((HttpStatusCode.OK, BankList(
            ("Wema Bank", "wema-bank", "035"),
            ("Wema Bank", "wema-bank", "035"),
            ("GTBank", "guaranty-trust-bank", "058"))));

        var banks = await client.ListAllBanksAsync(CancellationToken.None);

        Assert.Equal(2, banks.Count);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task ListAllBanks_FailedResponse_ReturnsEmpty()
    {
        var (client, _) = Paged((HttpStatusCode.BadGateway, "bad gateway"));

        var banks = await client.ListAllBanksAsync(CancellationToken.None);

        Assert.Empty(banks);
    }
}
