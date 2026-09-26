using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Features.Auth.Commands.RequestVendorOtp;
using InstaSafe.Application.Features.Auth.Commands.VerifyVendorOtp;
using InstaSafe.Application.Mapping;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.UnitTests;

public class AuthHandlerTests
{
    private sealed class FakeVendors : IVendorRepository
    {
        public List<Vendor> Vendors { get; } = new();
        public Task<Vendor?> GetByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult(Vendors.FirstOrDefault(v => v.Id == id));
        public Task<Vendor?> GetByPhoneAsync(string phone, CancellationToken ct)
            => Task.FromResult(Vendors.FirstOrDefault(v => v.Phone == phone));
        public Task<Vendor?> GetByEmailAsync(string email, CancellationToken ct)
            => Task.FromResult(Vendors.FirstOrDefault(v =>
                v.Email != null && v.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));
        public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct)
            => Task.FromResult(Vendors.Any(v =>
                v.Email != null && v.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));
        public Task<bool> ExistsByPhoneAsync(string phone, CancellationToken ct)
            => Task.FromResult(Vendors.Any(v => v.Phone == phone));
        public Task AddAsync(Vendor vendor, CancellationToken ct)
        {
            Vendors.Add(vendor);
            return Task.CompletedTask;
        }
        public Task<List<Vendor>> ListAsync(int page, int pageSize, bool? activeOnly, CancellationToken ct)
            => Task.FromResult(Vendors.ToList());
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeOtp : IOtpService
    {
        public string NextCode { get; set; } = "123456";
        public string GenerateOtp(int digits = 6) => NextCode;
        public string NewSalt() => "testsalt12345678";
        public string Hash(string otp, string salt) => $"HASH:{salt}:{otp}";
    }

    private sealed class FakeWhatsApp : IWhatsAppSender
    {
        public List<(string To, string Body)> Sent { get; } = new();
        public Task SendTextAsync(string toPhone, string body, CancellationToken ct)
        {
            Sent.Add((toPhone, body));
            return Task.CompletedTask;
        }
        public Task SendTemplateAsync(string toPhone, string templateName, Dictionary<string, string> vars, CancellationToken ct)
            => Task.CompletedTask;
    }

    private sealed class FakeTokens : IJwtTokenService
    {
        public string CreateVendorToken(Guid vendorId, string phone) => $"TEST-TOKEN:{vendorId}";
        public string CreateDispatcherToken(Guid dispatcherId, string phone) => $"TEST-DRIVER-TOKEN:{dispatcherId}";
    }

    private sealed class FakeConfig : Microsoft.Extensions.Configuration.IConfiguration
    {
        public string? this[string key] { get => null; set { } }
        public IEnumerable<Microsoft.Extensions.Configuration.IConfigurationSection> GetChildren() => [];
        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => throw new NotImplementedException();
        public Microsoft.Extensions.Configuration.IConfigurationSection GetSection(string key) => throw new NotImplementedException();
    }

    private static IMapper Mapper()
    {
        var config = new MapperConfiguration(
            cfg => cfg.AddProfile<VendorMappingProfile>(),
            NullLoggerFactory.Instance);
        return config.CreateMapper();
    }

    [Fact]
    public async Task RequestCode_UnknownPhone_Fails()
    {
        var handler = new RequestVendorOtpCommandHandler(
            new FakeVendors(), new FakeOtp(), new FakeWhatsApp(), new FakeConfig(),
            NullLogger<RequestVendorOtpCommandHandler>.Instance);

        var result = await handler.Handle(new RequestVendorOtpCommand("08019999999"), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task RequestCode_KnownVendor_StoresOtp_AndSendsWhatsApp()
    {
        var vendors = new FakeVendors();
        vendors.Vendors.Add(new Vendor { Phone = "2348012345678", DisplayName = "Ada" });
        var wa = new FakeWhatsApp();
        var handler = new RequestVendorOtpCommandHandler(
            vendors, new FakeOtp(), wa, new FakeConfig(),
            NullLogger<RequestVendorOtpCommandHandler>.Instance);

        var result = await handler.Handle(new RequestVendorOtpCommand("08012345678"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(vendors.Vendors[0].OtpHash);
        Assert.NotNull(vendors.Vendors[0].OtpExpiresAt);
        Assert.Single(wa.Sent);
        Assert.Contains("123456", wa.Sent[0].Body);
    }

    [Fact]
    public async Task VerifyCode_CorrectCode_ReturnsToken_AndClearsOtp()
    {
        var vendors = new FakeVendors();
        var vendor = new Vendor { Phone = "2348012345678", DisplayName = "Ada" };
        vendors.Vendors.Add(vendor);
        var otp = new FakeOtp();
        var request = new RequestVendorOtpCommandHandler(
            vendors, otp, new FakeWhatsApp(), new FakeConfig(),
            NullLogger<RequestVendorOtpCommandHandler>.Instance);
        await request.Handle(new RequestVendorOtpCommand("08012345678"), CancellationToken.None);

        var verify = new VerifyVendorOtpCommandHandler(
            vendors, otp, new FakeTokens(), Mapper(), new FakeConfig());
        var result = await verify.Handle(new VerifyVendorOtpCommand("08012345678", "123456"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.StartsWith("TEST-TOKEN:", result.Value!.Token);
        Assert.Equal("Ada", result.Value.Vendor.DisplayName);
        Assert.Null(vendor.OtpHash);
    }

    [Fact]
    public async Task VerifyCode_WrongCode_IncrementsAttempts()
    {
        var vendors = new FakeVendors();
        vendors.Vendors.Add(new Vendor { Phone = "2348012345678", DisplayName = "Ada" });
        var otp = new FakeOtp();
        var request = new RequestVendorOtpCommandHandler(
            vendors, otp, new FakeWhatsApp(), new FakeConfig(),
            NullLogger<RequestVendorOtpCommandHandler>.Instance);
        await request.Handle(new RequestVendorOtpCommand("08012345678"), CancellationToken.None);

        var verify = new VerifyVendorOtpCommandHandler(
            vendors, otp, new FakeTokens(), Mapper(), new FakeConfig());

        await Assert.ThrowsAsync<DomainValidationException>(
            () => verify.Handle(new VerifyVendorOtpCommand("08012345678", "000000"), CancellationToken.None));
        Assert.Equal(1, vendors.Vendors[0].OtpAttempts);
    }

    [Fact]
    public async Task VerifyCode_WithoutRequest_Fails()
    {
        var vendors = new FakeVendors();
        vendors.Vendors.Add(new Vendor { Phone = "2348012345678", DisplayName = "Ada" });
        var verify = new VerifyVendorOtpCommandHandler(
            vendors, new FakeOtp(), new FakeTokens(), Mapper(), new FakeConfig());

        var result = await verify.Handle(new VerifyVendorOtpCommand("08012345678", "123456"), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}
