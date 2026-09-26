using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Features.Vendors.Commands.UpdateVendorPhone;
using InstaSafe.Application.Mapping;
using InstaSafe.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.UnitTests;

public class UpdateVendorPhoneTests
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

    private sealed class PassSanitizer : ISanitizer
    {
        public string Clean(string? input, int maxLength = 2000)
            => string.IsNullOrEmpty(input) ? string.Empty : input.Length > maxLength ? input[..maxLength] : input;
    }

    private static UpdateVendorPhoneCommandHandler Handler(FakeVendors repo)
    {
        var config = new MapperConfiguration(
            cfg => cfg.AddProfile<VendorMappingProfile>(),
            NullLoggerFactory.Instance);
        return new UpdateVendorPhoneCommandHandler(repo, new PassSanitizer(), config.CreateMapper());
    }

    [Fact]
    public async Task UpdatePhone_NormalizesToCanonical()
    {
        var repo = new FakeVendors();
        var vendor = new Vendor { Phone = "2348012345678", DisplayName = "Ada" };
        repo.Vendors.Add(vendor);

        var result = await Handler(repo).Handle(
            new UpdateVendorPhoneCommand(vendor.Id, "07031602720"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("2347031602720", result.Value!.Phone);
    }

    [Fact]
    public async Task UpdatePhone_Duplicate_Fails()
    {
        var repo = new FakeVendors();
        var vendor = new Vendor { Phone = "2348012345678", DisplayName = "Ada" };
        repo.Vendors.Add(vendor);
        repo.Vendors.Add(new Vendor { Phone = "2347031602720", DisplayName = "Other" });

        var result = await Handler(repo).Handle(
            new UpdateVendorPhoneCommand(vendor.Id, "07031602720"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("already in use", result.Error);
    }

    [Fact]
    public async Task UpdatePhone_MissingVendor_Fails()
    {
        var result = await Handler(new FakeVendors()).Handle(
            new UpdateVendorPhoneCommand(Guid.NewGuid(), "07031602720"), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}
