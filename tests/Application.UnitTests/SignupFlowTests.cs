using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Features.Auth.Commands.RequestEmailCode;
using InstaSafe.Application.Features.Auth.Commands.VendorLogin;
using InstaSafe.Application.Features.Auth.Commands.VerifyEmailCode;
using InstaSafe.Application.Features.Vendors.Commands.RegisterVendor;
using InstaSafe.Application.Mapping;
using InstaSafe.Domain.Entities;
using InstaSafe.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.UnitTests;

public class SignupFlowTests
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
        public Task<bool> ExistsByPhoneAsync(string phone, CancellationToken ct)
            => Task.FromResult(Vendors.Any(v => v.Phone == phone));
        public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct)
            => Task.FromResult(Vendors.Any(v =>
                v.Email != null && v.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));
        public Task AddAsync(Vendor vendor, CancellationToken ct)
        {
            Vendors.Add(vendor);
            return Task.CompletedTask;
        }
        public Task<List<Vendor>> ListAsync(int page, int pageSize, bool? activeOnly, CancellationToken ct)
            => Task.FromResult(Vendors.ToList());
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakePaystack : IPaystackClient
    {
        public Task<(string Reference, string AuthUrl)> InitializeTransactionAsync(
            string email, long amountKobo, Guid orderId, CancellationToken ct)
            => Task.FromResult(("ref", "https://pay.test"));
        public Task<bool> VerifyTransactionAsync(string reference, CancellationToken ct)
            => Task.FromResult(true);
        public Task<string?> CreateRecipientAsync(string accountNumber, string bankCode, string name, CancellationToken ct)
            => Task.FromResult<string?>("RCP_TEST");
        public Task<string?> InitiateTransferAsync(long amountKobo, string recipientCode, string reason, CancellationToken ct)
            => Task.FromResult<string?>(null);
        public Task<(bool Success, string? RefundReference, string? Error)> RefundTransactionAsync(string reference, CancellationToken ct)
            => Task.FromResult((true, (string?)"RFND_TEST", (string?)null));
        public Task<(bool Success, string? CustomerCode, string? Error)> CreateCustomerAsync(string email, string firstName, string lastName, string phone, Guid orderId, CancellationToken ct)
            => Task.FromResult((true, (string?)"CUS_TEST", (string?)null));
        public Task<(bool Success, string? AccountNumber, string? AccountName, string? Bank, string? Error)> AssignDedicatedAccountAsync(string customerCode, string? preferredBank, CancellationToken ct)
            => Task.FromResult((true, (string?)"0123456789", (string?)"Ada Obi", (string?)"Wema", (string?)null));
        public Task<List<(string Name, string Slug, string Code)>> ListTransferBanksAsync(CancellationToken ct)
            => Task.FromResult(new List<(string Name, string Slug, string Code)>());
        public Task<AccountResolveResult> ResolveAccountAsync(
            string accountNumber, string bankCode, CancellationToken ct)
            => Task.FromResult(new AccountResolveResult(true, "Ada Obi", ResolveFailureKind.Invalid, ""));
        public Task<List<(string Name, string Slug, string Code)>> ListAllBanksAsync(CancellationToken ct)
            => Task.FromResult(new List<(string Name, string Slug, string Code)>());
    }

    private sealed class PassSanitizer : ISanitizer
    {
        public string Clean(string? input, int maxLength = 2000)
            => string.IsNullOrEmpty(input) ? string.Empty : input.Length > maxLength ? input[..maxLength] : input;
    }
    private sealed class FakeOtp : IOtpService
    {
        public string GenerateOtp(int digits = 6) => "654321";
        public string NewSalt() => "testsalt12345678";
        public string Hash(string otp, string salt) => $"HASH:{salt}:{otp}";
    }

    private sealed class FakeEmail : IEmailSender
    {
        public bool IsConfigured => true;
        public List<string> Sent { get; } = new();
        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
        {
            Sent.Add(toEmail);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeConfig : Microsoft.Extensions.Configuration.IConfiguration
    {
        public string? this[string key] { get => null; set { } }
        public IEnumerable<Microsoft.Extensions.Configuration.IConfigurationSection> GetChildren() => [];
        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => throw new NotImplementedException();
        public Microsoft.Extensions.Configuration.IConfigurationSection GetSection(string key) => throw new NotImplementedException();
    }

    private readonly FakeVendors _vendors = new();
    private readonly FakeEmail _email = new();
    private readonly IMapper _mapper;

    public SignupFlowTests()
    {
        var config = new MapperConfiguration(
            cfg => cfg.AddProfile<VendorMappingProfile>(),
            NullLoggerFactory.Instance);
        _mapper = config.CreateMapper();
    }

    private async Task<InstaSafe.Application.Common.Models.Result<InstaSafe.Application.Features.Vendors.DTOs.VendorDto>> Signup()
    {
        var handler = new RegisterVendorCommandHandler(
            _vendors, new PassSanitizer(), new PasswordHasher(),
            new FakeOtp(), _email, new FakeConfig(),
            NullLogger<RegisterVendorCommandHandler>.Instance, _mapper);
        return await handler.Handle(
            new RegisterVendorCommand("08012345678", "Ada Boutique", "Ada", "Obi",
                "ada@example.com", "s3cretPass!"),
            CancellationToken.None);
    }

    [Fact]
    public async Task FullSignup_VerifyEmail_LoginByPhoneAndEmail()
    {
        var signup = await Signup();
        Assert.True(signup.IsSuccess);
        var vendor = _vendors.Vendors.Single();
        Assert.False(vendor.EmailVerified);
        Assert.False(vendor.OnboardingCompleted);
        Assert.NotEqual("s3cretPass!", vendor.PasswordHash);
        Assert.Single(_email.Sent);

        var verifier = new VerifyEmailCodeCommandHandler(_vendors, new FakeOtp(), _mapper);
        var verified = await verifier.Handle(
            new VerifyEmailCodeCommand("ada@example.com", "654321"), CancellationToken.None);
        Assert.True(verified.IsSuccess);
        Assert.True(verified.Value!.EmailVerified);

        var login = new VendorLoginCommandHandler(
            _vendors, new PasswordHasher(), new AgroTokens(), _mapper, new FakeConfig());

        var byPhone = await login.Handle(new VendorLoginCommand("08012345678", "s3cretPass!"), CancellationToken.None);
        Assert.True(byPhone.IsSuccess);
        Assert.StartsWith("TEST-TOKEN:", byPhone.Value!.Token);

        var byEmail = await login.Handle(new VendorLoginCommand("ADA@EXAMPLE.COM", "s3cretPass!"), CancellationToken.None);
        Assert.True(byEmail.IsSuccess);

        var wrong = await login.Handle(new VendorLoginCommand("08012345678", "nope-nope-nope"), CancellationToken.None);
        Assert.False(wrong.IsSuccess);
    }

    private sealed class AgroTokens : IJwtTokenService
    {
        public string CreateVendorToken(Guid vendorId, string phone) => $"TEST-TOKEN:{vendorId}";
        public string CreateDispatcherToken(Guid dispatcherId, string phone) => $"TEST-DRIVER:{dispatcherId}";
        public string CreateAdminToken(string email) => $"TEST-ADMIN:{email}";
    }

    [Fact]
    public void PasswordHasher_RoundTrips_AndRejectsGarbage()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("s3cretPass!");
        Assert.True(hasher.Verify("s3cretPass!", hash));
        Assert.False(hasher.Verify("wrong", hash));
        Assert.False(hasher.Verify("s3cretPass!", "not-a-hash"));
        Assert.False(hasher.Verify("s3cretPass!", null));
    }
}
