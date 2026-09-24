using InstaSafe.Application.Features.Vendors.Commands.RegisterVendor;
using InstaSafe.Application.Features.Vendors.Commands.UpdateVendorPayout;
using InstaSafe.Application.Features.Vendors.Commands.UpdateVendorProfile;
using InstaSafe.Application.Common.Helpers;

namespace Application.UnitTests;

public class VendorValidatorTests
{
    [Theory]
    [InlineData("", "Ada")]
    [InlineData("08012345678", "")]
    public void RegisterVendor_RejectsEmptyRequiredFields(string phone, string displayName)
    {
        var validator = new RegisterVendorCommandValidator();
        var result = validator.Validate(new RegisterVendorCommand(phone, displayName));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void RegisterVendor_AcceptsValidMinimalVendor()
    {
        var validator = new RegisterVendorCommandValidator();
        var result = validator.Validate(new RegisterVendorCommand("08012345678", "Ada Boutique"));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void RegisterVendor_BankPairRequiresBothParts()
    {
        var validator = new RegisterVendorCommandValidator();
        var result = validator.Validate(new RegisterVendorCommand(
            "08012345678", "Ada", AccountNumber: "0123456789", BankCode: null));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void UpdatePayout_RequiresAccountAndBank()
    {
        var validator = new UpdateVendorPayoutCommandValidator();
        var result = validator.Validate(new UpdateVendorPayoutCommand(Guid.NewGuid(), "", ""));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void UpdateProfile_RequiresDisplayName()
    {
        var validator = new UpdateVendorProfileCommandValidator();
        var result = validator.Validate(new UpdateVendorProfileCommand(Guid.NewGuid(), ""));
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("+234 801 234 5678", "2348012345678")]
    [InlineData("2348012345678@c.us", "2348012345678")]
    [InlineData("0801-234-5678", "08012345678")]
    public void PhoneNormalizer_StripsFormattingAndJidSuffix(string raw, string expected)
    {
        Assert.Equal(expected, PhoneNormalizer.Normalize(raw));
    }
}
