using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Features.Dispatch.Commands.RequestDispatcherOtp;
using InstaSafe.Application.Features.Dispatch.Commands.VerifyDispatcherOtp;
using InstaSafe.Application.Mapping;
using InstaSafe.Domain.Entities;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.UnitTests;

public class DispatchOtpTests
{
    private sealed class FakeDispatchers : IDispatcherRepository
    {
        public List<Dispatcher> Rows { get; } = new();
        public Task<Dispatcher?> GetByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult(Rows.FirstOrDefault(d => d.Id == id));
        public Task<Dispatcher?> GetByPhoneAsync(string phone, CancellationToken ct)
            => Task.FromResult(Rows.FirstOrDefault(d => d.Phone == phone));
        public Task<bool> ExistsByPhoneAsync(string phone, CancellationToken ct)
            => Task.FromResult(Rows.Any(d => d.Phone == phone));
        public Task AddAsync(Dispatcher dispatcher, CancellationToken ct)
        {
            Rows.Add(dispatcher);
            return Task.CompletedTask;
        }
        public Task<List<Dispatcher>> ListAsync(int page, int pageSize, bool? activeOnly, CancellationToken ct)
            => Task.FromResult(Rows.ToList());
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeOtp : IOtpService
    {
        public string GenerateOtp(int digits = 6) => "777777";
        public string NewSalt() => "testsalt12345678";
        public string Hash(string otp, string salt) => $"HASH:{salt}:{otp}";
    }

    private sealed class FakeWa : IWhatsAppSender
    {
        public List<string> Sent { get; } = new();
        public Task SendTextAsync(string toPhone, string body, CancellationToken ct)
        {
            Sent.Add(body);
            return Task.CompletedTask;
        }
        public Task SendTemplateAsync(string toPhone, string templateName, Dictionary<string, string> vars, CancellationToken ct)
            => Task.CompletedTask;
    }

    private sealed class FakeConfig : Microsoft.Extensions.Configuration.IConfiguration
    {
        public string? this[string key] { get => null; set { } }
        public IEnumerable<Microsoft.Extensions.Configuration.IConfigurationSection> GetChildren() => [];
        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => throw new NotImplementedException();
        public Microsoft.Extensions.Configuration.IConfigurationSection GetSection(string key) => throw new NotImplementedException();
    }

    private sealed class FakeTokens : IJwtTokenService
    {
        public string CreateVendorToken(Guid vendorId, string phone) => "V";
        public string CreateDispatcherToken(Guid dispatcherId, string phone) => $"DRIVER:{dispatcherId}";
        public string CreateAdminToken(string email) => $"ADMIN:{email}";
    }

    [Fact]
    public async Task RequestCode_AutoProvisionsUnknownPhone_AndSendsCode()
    {
        var repo = new FakeDispatchers();
        var wa = new FakeWa();
        var handler = new RequestDispatcherOtpCommandHandler(
            repo, new FakeOtp(), wa, new FakeConfig(),
            NullLogger<RequestDispatcherOtpCommandHandler>.Instance);

        var result = await handler.Handle(new RequestDispatcherOtpCommand("08055556666"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(repo.Rows);
        Assert.NotNull(repo.Rows[0].OtpHash);
        Assert.Single(wa.Sent);
        Assert.Contains("777777", wa.Sent[0]);
    }

    [Fact]
    public async Task VerifyCode_CorrectCode_ReturnsDriverToken()
    {
        var repo = new FakeDispatchers();
        var request = new RequestDispatcherOtpCommandHandler(
            repo, new FakeOtp(), new FakeWa(), new FakeConfig(),
            NullLogger<RequestDispatcherOtpCommandHandler>.Instance);
        await request.Handle(new RequestDispatcherOtpCommand("08055556666"), CancellationToken.None);

        var config = new MapperConfiguration(
            cfg => cfg.AddProfile<DispatcherMappingProfile>(),
            NullLoggerFactory.Instance);
        var verify = new VerifyDispatcherOtpCommandHandler(
            repo, new FakeOtp(), new FakeTokens(), config.CreateMapper(), new FakeConfig());
        var result = await verify.Handle(
            new VerifyDispatcherOtpCommand("08055556666", "777777"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.StartsWith("DRIVER:", result.Value!.Token);
    }
}
