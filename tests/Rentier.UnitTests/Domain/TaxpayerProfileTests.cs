using FluentAssertions;
using Rentier.Domain.Entities;
using Rentier.Domain.Exceptions;
using Xunit;

namespace Rentier.UnitTests;

public class TaxpayerProfileTests
{
    private readonly Guid TestId = Guid.NewGuid();
    private const string ValidJmbg = "1234567890123";
    private const string ValidFullName = "Marko Marković";
    private const string ValidAddress = "Knez Mihailova 1, Beograd";
    private const string ValidOpstinaCode = "7101";

    [Fact]
    public void Constructor_ValidArguments_CreatesProfile()
    {
        var profile = new TaxpayerProfile(TestId, ValidJmbg, ValidFullName, ValidAddress, ValidOpstinaCode);
        profile.Id.Should().Be(TestId);
        profile.Jmbg.Should().Be(ValidJmbg);
        profile.FullName.Should().Be(ValidFullName);
        profile.Address.Should().Be(ValidAddress);
        profile.OpstinaCode.Should().Be(ValidOpstinaCode);
        profile.PhoneNumber.Should().BeNull();
        profile.Email.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithOptionalFields_SetsPhoneAndEmail()
    {
        var profile = new TaxpayerProfile(TestId, ValidJmbg, ValidFullName, ValidAddress, ValidOpstinaCode,
            phoneNumber: "+381641234567", email: "marko@example.com");
        profile.PhoneNumber.Should().Be("+381641234567");
        profile.Email.Should().Be("marko@example.com");
    }

    [Theory]
    [InlineData("123456789012")]   // 12 digits — too short
    [InlineData("12345678901234")] // 14 digits — too long
    [InlineData("123456789012a")]  // non-numeric
    [InlineData("")]               // empty
    [InlineData("   ")]            // whitespace
    public void Constructor_InvalidJmbg_ThrowsDomainException(string badJmbg)
    {
        var act = () => new TaxpayerProfile(TestId, badJmbg, ValidFullName, ValidAddress, ValidOpstinaCode);
        act.Should().Throw<DomainException>().WithMessage("*JMBG*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespaceFullName_ThrowsDomainException(string? badFullName)
    {
        var act = () => new TaxpayerProfile(TestId, ValidJmbg, badFullName!, ValidAddress, ValidOpstinaCode);
        act.Should().Throw<DomainException>().WithMessage("*FullName*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespaceAddress_ThrowsDomainException(string? badAddress)
    {
        var act = () => new TaxpayerProfile(TestId, ValidJmbg, ValidFullName, badAddress!, ValidOpstinaCode);
        act.Should().Throw<DomainException>().WithMessage("*Address*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespaceOpstinaCode_ThrowsDomainException(string? badCode)
    {
        var act = () => new TaxpayerProfile(TestId, ValidJmbg, ValidFullName, ValidAddress, badCode!);
        act.Should().Throw<DomainException>().WithMessage("*OpstinaCode*");
    }

    [Fact]
    public void Constructor_NullPhoneAndEmail_AreAllowed()
    {
        var profile = new TaxpayerProfile(TestId, ValidJmbg, ValidFullName, ValidAddress, ValidOpstinaCode,
            phoneNumber: null, email: null);
        profile.PhoneNumber.Should().BeNull();
        profile.Email.Should().BeNull();
    }
}
