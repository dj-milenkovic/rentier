using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Rentier.Application.Commands;
using Rentier.Application.Common;
using Rentier.Application.Handlers;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.ValueObjects;
using Xunit;

namespace Rentier.Scenarios.Tests;

/// <summary>
/// The Filings page resolves its command handlers once and keeps them for as long as the page
/// lives, so every handler — and the DbContext behind its repository — is reused across many
/// user actions. These scenarios drive the production DI registration the same way (issue #119):
/// a handler must always act on the filing as it is in the database, never on a copy its
/// context cached during an earlier action.
/// </summary>
[Trait("Category", "Integration")]
public sealed class LongLivedHandlersScenario : IAsyncLifetime
{
    private readonly ScenarioFixture _fixture = new();
    private UpdateFilingStatusCommandHandler _statusHandler = null!;
    private UpdatePaymentReferenceCommandHandler _referenceHandler = null!;

    public async ValueTask InitializeAsync()
    {
        await _fixture.InitializeAsync();

        // Resolved once and reused, exactly like FilingsHandlers inside FilingsViewModel.
        _statusHandler = new UpdateFilingStatusCommandHandler(_fixture.Get<IFilingRepository>());
        _referenceHandler = new UpdatePaymentReferenceCommandHandler(_fixture.Get<IFilingRepository>());
    }

    public ValueTask DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task UpdatePaymentReference_AfterFilingWasPaidByAnotherHandler_KeepsPaidStatus()
    {
        var ct = TestContext.Current.CancellationToken;
        var filingId = await SeedFilingAsync(ct);

        (await AdvanceAsync(filingId, FilingStatus.Filed, ct)).IsSuccess.Should().BeTrue();
        (await SetReferenceAsync(filingId, "97 1234", ct)).IsSuccess.Should().BeTrue();
        (await AdvanceAsync(filingId, FilingStatus.Paid, ct)).IsSuccess.Should().BeTrue();

        var result = await SetReferenceAsync(filingId, "97 5678", ct);

        result.IsSuccess.Should().BeTrue();
        var stored = await ReadFreshAsync(filingId, ct);
        stored.Status.Should().Be(FilingStatus.Paid, "editing the payment reference must not change the status");
        stored.PaymentReference.Should().Be("97 5678");
    }

    [Fact]
    public async Task UpdateFilingStatus_AfterReferenceEditedByAnotherHandler_KeepsTheNewReference()
    {
        var ct = TestContext.Current.CancellationToken;
        var filingId = await SeedFilingAsync(ct);

        (await AdvanceAsync(filingId, FilingStatus.Filed, ct)).IsSuccess.Should().BeTrue();
        (await SetReferenceAsync(filingId, "97 1234", ct)).IsSuccess.Should().BeTrue();

        var result = await AdvanceAsync(filingId, FilingStatus.Paid, ct);

        result.IsSuccess.Should().BeTrue();
        var stored = await ReadFreshAsync(filingId, ct);
        stored.Status.Should().Be(FilingStatus.Paid);
        stored.PaymentReference.Should().Be("97 1234", "advancing the status must not erase the reference");
    }

    private Task<Result<VoidResult, Error>> AdvanceAsync(Guid id, FilingStatus status, CancellationToken ct)
        => _statusHandler.HandleAsync(new UpdateFilingStatusCommand(id, status), ct);

    private Task<Result<VoidResult, Error>> SetReferenceAsync(Guid id, string reference, CancellationToken ct)
        => _referenceHandler.HandleAsync(new UpdatePaymentReferenceCommand(id, reference), ct);

    /// <summary>Reads through a brand-new repository (and DbContext), i.e. what the page reload sees.</summary>
    private async Task<Filing> ReadFreshAsync(Guid id, CancellationToken ct)
    {
        var filing = await _fixture.Services.GetRequiredService<IFilingRepository>().GetByIdAsync(id, ct);
        filing.Should().NotBeNull();
        return filing!;
    }

    private async Task<Guid> SeedFilingAsync(CancellationToken ct)
    {
        var profile = new TaxpayerProfile(
            id: Guid.NewGuid(),
            jmbg: "1234567890123",
            fullName: "Test User",
            address: "Test Address",
            opstinaCode: "049");
        await _fixture.Get<ITaxpayerProfileRepository>().SaveAsync(profile, ct);

        var filing = Filing.CreateFromIncome(
            new FilingInfo(
                incomeType: IncomeType.Dividend,
                payingEntity: "Test Corp",
                incomeDate: new DateOnly(2024, 6, 15),
                grossIncomeRsd: 100_000m,
                whtPaidRsd: 15_000m,
                grossTaxPayableRsd: 15_000m,
                taxPayableRsd: 0m),
            taxpayerProfileId: profile.Id,
            filingDeadline: new DateOnly(2024, 7, 30),
            provenance: new FilingProvenance());
        await _fixture.Get<IFilingRepository>().AddAsync(filing, ct);
        return filing.Id;
    }
}
