using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Vendors.Commands.RegisterVendor;
using InstaSafe.Application.Features.Vendors.DTOs;
using InstaSafe.Application.Mapping;
using InstaSafe.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.UnitTests;

public class RegisterVendorHandlerTests
{
    private sealed class FakeVendorRepository : IVendorRepository
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
        {
            var q = Vendors.AsEnumerable();
            if (activeOnly == true) q = q.Where(v => v.IsActive);
            return Task.FromResult(q.Skip((page - 1) * pageSize).Take(pageSize).ToList());
        }
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakePaystack : IPaystackClient
    {
        public string? LastRecipientName { get; private set; }
        public Task<(string Reference, string AuthUrl)> InitializeTransactionAsync(
            string email, long amountKobo, Guid orderId, CancellationToken ct,
            string? callbackUrl = null)
            => Task.FromResult(("ref", "https://pay.test"));
        public Task<bool> VerifyTransactionAsync(string reference, CancellationToken ct)
            => Task.FromResult(true);
        public Task<string?> CreateRecipientAsync(string accountNumber, string bankCode, string name, CancellationToken ct)
        {
            LastRecipientName = name;
            return Task.FromResult<string?>("RCP_TEST");
        }
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

    private sealed class PassThroughSanitizer : ISanitizer
    {
        public string Clean(string? input, int maxLength = 2000)
            => input is null ? string.Empty : input.Length > maxLength ? input[..maxLength] : input;
    }
    private sealed class FakePasswords : IPasswordHasher
    {
        public string Hash(string password) => $"HASHED:{password}";
        public bool Verify(string password, string? hash) => hash == $"HASHED:{password}";
    }

    private sealed class FakeOtp : IOtpService
    {
        public string GenerateOtp(int digits = 6) => "123456";
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

    private sealed class HandlerDeps
    {
        public FakeEmail Email { get; } = new();
    }

    private static (RegisterVendorCommandHandler Handler, HandlerDeps Deps) CreateHandler(
        FakeVendorRepository repo)
    {
        var config = new MapperConfiguration(
            cfg => cfg.AddProfile<VendorMappingProfile>(),
            NullLoggerFactory.Instance);
        var deps = new HandlerDeps();
        var handler = new RegisterVendorCommandHandler(
            repo, new PassThroughSanitizer(), new FakePasswords(),
            new FakeOtp(), deps.Email, new FakeConfig(),
            NullLogger<RegisterVendorCommandHandler>.Instance, config.CreateMapper());
        return (handler, deps);
    }

    private static RegisterVendorCommand Signup(string phone = "08012345678") =>
        new(phone, "Ada Boutique", "Ada", "Obi", "ada@example.com", "s3cretPass!");

    [Fact]
    public async Task Register_NewVendor_Succeeds_Unverified_AndOnboardingOpen()
    {
        var repo = new FakeVendorRepository();
        var (handler, deps) = CreateHandler(repo);

        var result = await handler.Handle(Signup(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Ada Boutique", result.Value!.DisplayName);
        Assert.True(result.Value.IsActive);
        Assert.False(result.Value.EmailVerified);
        Assert.False(result.Value.OnboardingCompleted);
        Assert.Null(result.Value.PaystackRecipientCode);
        Assert.NotNull(repo.Vendors[0].PasswordHash);
        Assert.NotEqual("s3cretPass!", repo.Vendors[0].PasswordHash);
        Assert.NotNull(repo.Vendors[0].EmailOtpHash);
        Assert.Single(repo.Vendors);
        Assert.Single(deps.Email.Sent);
    }

    [Fact]
    public async Task Register_DuplicatePhone_Fails()
    {
        var repo = new FakeVendorRepository();
        repo.Vendors.Add(new Vendor { Phone = "2348012345678", DisplayName = "Existing" });
        var (handler, _) = CreateHandler(repo);

        var result = await handler.Handle(Signup(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("already exists", result.Error);
        Assert.Single(repo.Vendors);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Fails()
    {
        var repo = new FakeVendorRepository();
        repo.Vendors.Add(new Vendor { Phone = "2348099999999", DisplayName = "Existing", Email = "ada@example.com" });
        var (handler, _) = CreateHandler(repo);

        var result = await handler.Handle(Signup(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("already exists", result.Error);
        Assert.Single(repo.Vendors);
    }

    [Fact]
    public async Task Register_SamePhoneDifferentFormat_FailsAfterNormalize()
    {
        var repo = new FakeVendorRepository();
        repo.Vendors.Add(new Vendor { Phone = "2348012345678", DisplayName = "Existing" });
        var (handler, _) = CreateHandler(repo);

        var result = await handler.Handle(
            Signup("+234 801 234 5678"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Single(repo.Vendors);
    }

    [Fact]
    public async Task Register_LeavesOnboardingOpen_ForPayoutStep()
    {
        var repo = new FakeVendorRepository();
        var (handler, _) = CreateHandler(repo);

        var result = await handler.Handle(Signup(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.PaystackRecipientCode);
        Assert.False(result.Value.OnboardingCompleted);
    }
}
