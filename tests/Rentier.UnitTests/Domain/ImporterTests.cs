using FluentAssertions;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;
using Xunit;

namespace Rentier.UnitTests;

public sealed class ImporterTests
{
    [Fact]
    public void Create_ValidDisplayName_ReturnsImporter()
    {
        var importer = Importer.Create("My Importer");
        importer.Id.Should().NotBeEmpty();
        importer.DisplayName.Should().Be("My Importer");
    }

    [Fact]
    public void Create_EmptyDisplayName_ThrowsDomainException()
    {
        var act = () => Importer.Create(string.Empty);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WhitespaceDisplayName_ThrowsDomainException()
    {
        var act = () => Importer.Create("   ");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_DisplayNameTooLong_ThrowsDomainException()
    {
        var longName = new string('x', 201);
        var act = () => Importer.Create(longName);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_DisplayName200Chars_Succeeds()
    {
        var name = new string('x', 200);
        var importer = Importer.Create(name);
        importer.DisplayName.Should().HaveLength(200);
    }

    [Fact]
    public void Create_SetsDefaultReportType()
    {
        var importer = Importer.Create("Test");
        importer.ReportType.Should().Be(ReportType.IbkrCsv);
    }

    [Fact]
    public void Create_GeneratesUniqueId()
    {
        var a = Importer.Create("Importer A");
        var b = Importer.Create("Importer B");
        a.Id.Should().NotBe(b.Id);
    }

    [Fact]
    public void UpdateDetails_ValidInputs_UpdatesAllFields()
    {
        var importer = Importer.Create("Original");
        var profileId = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();

        importer.UpdateDetails(
            "Updated",
            ReportType.IbkrCsv,
            profileId,
            mailboxId,
            "from@example.com",
            "Subject:",
            @"\d+\.csv",
            "Payment notes here");

        importer.DisplayName.Should().Be("Updated");
        importer.ReportType.Should().Be(ReportType.IbkrCsv);
        importer.TaxpayerProfileId.Should().Be(profileId);
        importer.MailboxId.Should().Be(mailboxId);
        importer.FromFilter.Should().Be("from@example.com");
        importer.SubjectFilter.Should().Be("Subject:");
        importer.AttachmentRegex.Should().Be(@"\d+\.csv");
        importer.PaymentNotes.Should().Be("Payment notes here");
    }

    [Fact]
    public void UpdateDetails_EmptyDisplayName_ThrowsDomainException()
    {
        var importer = Importer.Create("Test");
        var act = () => importer.UpdateDetails(string.Empty, ReportType.IbkrCsv, null, null, "", "", "", "");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void UpdateDetails_NullForeignKeys_AcceptsNulls()
    {
        var importer = Importer.Create("Test");
        var act = () => importer.UpdateDetails("Test", ReportType.IbkrCsv, null, null, "", "", "", "");
        act.Should().NotThrow();
        importer.TaxpayerProfileId.Should().BeNull();
        importer.MailboxId.Should().BeNull();
    }

    [Fact]
    public void UpdateDetails_NullFilters_StoresEmptyString()
    {
        var importer = Importer.Create("Test");
        importer.UpdateDetails("Test", ReportType.IbkrCsv, null, null, null!, null!, null!, null!);
        importer.FromFilter.Should().Be(string.Empty);
        importer.SubjectFilter.Should().Be(string.Empty);
        importer.AttachmentRegex.Should().Be(string.Empty);
        importer.PaymentNotes.Should().Be(string.Empty);
    }

    [Fact]
    public void UpdateDetails_WithDisplayNameExceedingMaxLength_ThrowsDomainException()
    {
        var importer = Importer.Create("Test");
        var longName = new string('x', 201);

        var act = () => importer.UpdateDetails(longName, ReportType.IbkrCsv, null, null, "", "", "", "");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WithDisplayNameRequiringTrim_TrimsDisplayName()
    {
        var importer = Importer.Create("  My Importer  ");

        importer.DisplayName.Should().Be("My Importer");
    }

    [Fact]
    public void UpdateDetails_WithPaymentNotesExactly4000Chars_Succeeds()
    {
        var importer = Importer.Create("Test");
        var notes = new string('x', 4000);

        var act = () => importer.UpdateDetails("Test", ReportType.IbkrCsv, null, null, "", "", "", notes);

        act.Should().NotThrow();
    }

    [Fact]
    public void UpdateDetails_WithPaymentNotesExceeding4000Chars_ThrowsDomainException()
    {
        var importer = Importer.Create("Test");
        var longNotes = new string('x', 4001);
        var act = () => importer.UpdateDetails("Test", ReportType.IbkrCsv, null, null, "", "", "", longNotes);
        act.Should().Throw<DomainException>();
    }
}
