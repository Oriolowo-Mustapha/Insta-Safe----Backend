using InstaSafe.Application.Features.Vendors.Commands.RegisterVendor;
using InstaSafe.Application.Features.Vendors.Commands.UpdateVendorPayout;
using InstaSafe.Application.Features.Vendors.Commands.UpdateVendorProfile;
using InstaSafe.Application.Common.Helpers;

namespace Application.UnitTests;

public class VendorValidatorTests
{
    private static RegisterVendorCommand Full(string phone = "08012345678") =>
        new(phone, "Ada Boutique", "Ada", "Obi", "ada@example.com", "s3cretPass!");

    [Theory]
    [InlineData("", "Ada Boutique", "Ada", "Obi", "ada@example.com", "s3cretPass!")]
    [InlineData("08012345678", "", "Ada", "Obi", "ada@example.com", "s3cretPass!")]
    [InlineData("08012345678", "Ada Boutique", "", "Obi", "ada@example.com", "s3cretPass!")]
    [InlineData("08012345678", "Ada Boutique", "Ada", "Obi", "not-an-email", "s3cretPass!")]
    [InlineData("08012345678", "Ada Boutique", "Ada", "Obi", "ada@example.com", "short")]
    public void RegisterVendor_RejectsBadSignup(
        string phone, string displayName, string first, string last, string email, string password)
    {
        var validator = new RegisterVendorCommandValidator();
        var result = validator.Validate(new RegisterVendorCommand(
            phone, displayName, first, last, email, password));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void RegisterVendor_AcceptsValidSignup()
    {
        var validator = new RegisterVendorCommandValidator();
        Assert.True(validator.Validate(Full()).IsValid);
    }

    [Fact]
    public void RegisterVendor_RejectsBadEmailAndShortPassword()
    {
        var validator = new RegisterVendorCommandValidator();
        Assert.False(validator.Validate(Full() with { Email = "not-an-email" }).IsValid);
        Assert.False(validator.Validate(Full() with { Password = "short" }).IsValid);
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
