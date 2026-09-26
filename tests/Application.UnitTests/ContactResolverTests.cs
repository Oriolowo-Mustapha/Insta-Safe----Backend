using InstaSafe.Infrastructure.ExternalServices;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text;

namespace Application.UnitTests;

public class ContactResolverTests
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
        private readonly bool _withCreds;
        public FakeConfig(bool withCreds = true) => _withCreds = withCreds;
        public string? this[string key]
        {
            get => key switch
            {
                "OpenWA:BaseUrl" => "http://openwa.test",
                "OpenWA:ApiKey" => _withCreds ? "k" : null,
                "OpenWA:SessionId" => _withCreds ? "s" : null,
                _ => null
            };
            set { }
        }
        public IEnumerable<Microsoft.Extensions.Configuration.IConfigurationSection> GetChildren() => [];
        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => throw new NotImplementedException();
        public Microsoft.Extensions.Configuration.IConfigurationSection GetSection(string key) => throw new NotImplementedException();
    }

    private static OpenWAContactResolver Client(HttpStatusCode status, string json, bool creds = true) =>
        new(new HttpClient(new StubHandler(status, json)),
            new FakeConfig(creds), NullLogger<OpenWAContactResolver>.Instance);

    [Fact]
    public async Task Resolve_ReturnsPhone()
    {
        var client = Client(HttpStatusCode.OK,
            """{"contactId":"91745383633143@lid","phone":"2348012345678"}""");

        Assert.Equal("2348012345678",
            await client.ResolvePhoneAsync("91745383633143@lid", CancellationToken.None));
    }

    [Fact]
    public async Task Resolve_NullPhone_ReturnsNull()
    {
        var client = Client(HttpStatusCode.OK,
            """{"contactId":"91745383633143@lid","phone":null}""");

        Assert.Null(await client.ResolvePhoneAsync("91745383633143@lid", CancellationToken.None));
    }

    [Fact]
    public async Task Resolve_HttpError_ReturnsNull()
    {
        var client = Client(HttpStatusCode.NotFound, "{}");

        Assert.Null(await client.ResolvePhoneAsync("91745383633143@lid", CancellationToken.None));
    }

    [Fact]
    public async Task Resolve_NoCreds_ReturnsNullWithoutCalling()
    {
        var client = Client(HttpStatusCode.OK, "{}", creds: false);

        Assert.Null(await client.ResolvePhoneAsync("91745383633143@lid", CancellationToken.None));
    }
}
