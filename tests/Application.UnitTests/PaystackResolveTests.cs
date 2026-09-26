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

        var (ok, name, error) = await client.ResolveAccountAsync("0123456789", "058", CancellationToken.None);

        Assert.True(ok);
        Assert.Equal("ADA OBI", name);
        Assert.Null(error);
    }

    [Fact]
    public async Task Resolve_BadAccount_ReturnsFriendlyError()
    {
        var client = Client(HttpStatusCode.BadRequest,
            """{"status":false,"message":"Invalid account"}""");

        var (ok, name, error) = await client.ResolveAccountAsync("000", "058", CancellationToken.None);

        Assert.False(ok);
        Assert.Null(name);
        Assert.Contains("Check the number", error);
    }
}
