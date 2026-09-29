using InstaSafe.Application.Common.Helpers;

namespace Application.UnitTests;

public class BankDirectoryTests
{
    private static readonly List<BankInfo> Banks = new()
    {
        new("Guaranty Trust Bank", "guaranty-trust-bank", "058"),
        new("Access Bank", "access-bank", "044"),
        new("First Bank of Nigeria", "first-bank-of-nigeria", "011"),
        new("Wema Bank", "wema-bank", "035"),
        new("United Bank For Africa", "united-bank-for-africa", "033")
    };

    [Theory]
    [InlineData("GTB", "058")]
    [InlineData("gtbank", "058")]
    [InlineData("Guaranty Trust Bank", "058")]
    [InlineData("guaranty", "058")]
    [InlineData("058", "058")]
    [InlineData("Access", "044")]
    [InlineData("first bank", "011")]
    [InlineData("uba", "033")]
    [InlineData("wema", "035")]
    public void Match_ResolvesNamesAliasesAndCodes(string input, string expectedCode)
    {
        Assert.Equal(expectedCode, BankDirectory.Match(input, Banks)!.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Bank of Nowhere")]
    [InlineData("99")]
    public void Match_Unknown_ReturnsNull(string input)
    {
        Assert.Null(BankDirectory.Match(input, Banks));
    }
}
