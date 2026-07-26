using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.Exceptions;
using Rentier.Domain.ValueObjects;
using Xunit;

namespace Rentier.UnitTests;

/// <summary>
/// Property-based tests for the Filing.SetPaymentReference invariants.
/// Covers the round-trip, max-length rejection, and whitespace-normalisation rules.
/// </summary>
public class PaymentReferenceProperties
{
    private static Filing MakeFiling() =>
        Filing.CreateFromIncome(
            new FilingInfo(
                incomeType: IncomeType.Dividend,
                payingEntity: "Test Corp",
                incomeDate: new DateOnly(2024, 1, 15),
                grossIncomeRsd: 1000m,
                whtPaidRsd: 150m,
                grossTaxPayableRsd: 150m,
                taxPayableRsd: 0m),
            taxpayerProfileId: Guid.NewGuid(),
            filingDeadline: new DateOnly(2024, 2, 14),
            provenance: new FilingProvenance());

    /// <summary>
    /// For any non-null, non-whitespace-only string with length ≤ 200 (after trimming),
    /// the stored payment reference must equal the trimmed input exactly.
    /// </summary>
    [Property]
    public Property SetPaymentReference_ValidString_RoundTripsWithoutAlteration(
        NonEmptyString rawInput)
    {
        // Trim first — SetPaymentReference normalises whitespace
        var value = rawInput.Get.Trim();

        // Skip strings that become empty after trim or exceed the max length
        if (string.IsNullOrEmpty(value) || value.Length > 200)
            return true.ToProperty(); // Not applicable — covered by other tests

        var filing = MakeFiling();
        filing.SetPaymentReference(value);

        return (filing.PaymentReference == value).ToProperty();
    }

    /// <summary>
    /// Any string whose trimmed length exceeds 200 characters must be rejected
    /// with a DomainException.
    /// </summary>
    [Property]
    public Property SetPaymentReference_StringExceedingMaxLength_IsRejected(
        PositiveInt extraInt)
    {
        // Generate strings of length 201–500
        var length = 201 + (extraInt.Get % 300);
        var tooLong = new string('x', length);

        var filing = MakeFiling();
        var threw = false;

        try { filing.SetPaymentReference(tooLong); }
        catch (DomainException) { threw = true; }

        return threw.ToProperty();
    }

    /// <summary>
    /// A whitespace-only string must be normalised to null (not stored as-is).
    /// </summary>
    [Fact]
    public void SetPaymentReference_WhitespaceOnly_IsNormalisedToNull()
    {
        var filing = MakeFiling();
        filing.SetPaymentReference("   ");
        filing.PaymentReference.Should().BeNull();
    }
}
