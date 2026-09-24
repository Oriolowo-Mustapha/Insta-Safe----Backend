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
            string email, long amountKobo, Guid orderId, CancellationToken ct)
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
    }

    private sealed class PassThroughSanitizer : ISanitizer
    {
        public string Clean(string? input, int maxLength = 2000)
            => input is null ? string.Empty : input.Length > maxLength ? input[..maxLength] : input;
    }

    private static RegisterVendorCommandHandler CreateHandler(FakeVendorRepository repo, FakePaystack paystack)
    {
        var config = new MapperConfiguration(
            cfg => cfg.AddProfile<VendorMappingProfile>(),
            NullLoggerFactory.Instance);
        return new RegisterVendorCommandHandler(repo, paystack, new PassThroughSanitizer(), config.CreateMapper());
    }

    [Fact]
    public async Task Register_NewVendor_Succeeds_AndStoresRecipient()
    {
        var repo = new FakeVendorRepository();
        var paystack = new FakePaystack();
        var handler = CreateHandler(repo, paystack);

        var result = await handler.Handle(
            new RegisterVendorCommand("08012345678", "Ada Boutique", "0123456789", "058"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Ada Boutique", result.Value!.DisplayName);
        Assert.Equal("RCP_TEST", result.Value.PaystackRecipientCode);
        Assert.True(result.Value.IsActive);
        Assert.Single(repo.Vendors);
        Assert.Equal("Ada Boutique", paystack.LastRecipientName);
    }

    [Fact]
    public async Task Register_DuplicatePhone_Fails()
    {
        var repo = new FakeVendorRepository();
        repo.Vendors.Add(new Vendor { Phone = "08012345678", DisplayName = "Existing" });
        var handler = CreateHandler(repo, new FakePaystack());

        var result = await handler.Handle(
            new RegisterVendorCommand("08012345678", "Duplicate"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("already exists", result.Error);
        Assert.Single(repo.Vendors);
    }

    [Fact]
    public async Task Register_SamePhoneDifferentFormat_FailsAfterNormalize()
    {
        var repo = new FakeVendorRepository();
        repo.Vendors.Add(new Vendor { Phone = "2348012345678", DisplayName = "Existing" });
        var handler = CreateHandler(repo, new FakePaystack());

        var result = await handler.Handle(
            new RegisterVendorCommand("+234 801 234 5678", "Duplicate"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Single(repo.Vendors);
    }

    [Fact]
    public async Task Register_WithoutBank_DoesNotCreateRecipient()
    {
        var repo = new FakeVendorRepository();
        var paystack = new FakePaystack();
        var handler = CreateHandler(repo, paystack);

        var result = await handler.Handle(
            new RegisterVendorCommand("08012345678", "No Bank Yet"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.PaystackRecipientCode);
        Assert.Null(paystack.LastRecipientName);
    }
}
