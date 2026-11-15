using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Rentier.Application.Interfaces;
using Rentier.Application.Repositories;
using Rentier.Domain.Entities;
using Rentier.Domain.Enums;
using Rentier.Domain.ValueObjects;
using Rentier.Infrastructure.Persistence;

namespace Rentier.Infrastructure.Tests.Persistence;

/// <summary>
/// Atomicity contract of <see cref="EfUnitOfWork"/> against real SQLite: repositories
/// obtained from a unit of work only stage changes, and one SaveChangesAsync persists all
/// of them or none.
/// </summary>
[Trait("Category", "Integration")]
public sealed class EfUnitOfWorkTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private DbContextOptions<AppDbContext> _options = null!;
    private EfUnitOfWorkFactory _factory = null!;
    private TaxpayerProfile _profile = null!;
    private Report _report = null!;

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        _connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await _connection.OpenAsync(ct);

        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _factory = new EfUnitOfWorkFactory(_options);

        _profile = new TaxpayerProfile(Guid.NewGuid(), "1234567890123", "John Doe", "Belgrade", "018");
        var importer = Importer.Create("Test Importer");
        _report = Report.Create(importer.Id, "statement.csv", [1, 2, 3], 100L);

        await using var seed = new AppDbContext(_options);
        await seed.Database.EnsureCreatedAsync(ct);
        seed.TaxpayerProfiles.Add(_profile);
        seed.Importers.Add(importer);
        seed.Reports.Add(_report);
        await seed.SaveChangesAsync(ct);
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    private Filing MakeFiling(string entity, Guid? profileId = null)
    {
        var date = new DateOnly(2024, 6, 15);
        return Filing.CreateFromIncome(
            new FilingInfo(IncomeType.Dividend, entity, date, 1000m, 150m, 150m, 0m),
            profileId ?? _profile.Id, date.AddDays(30), new FilingProvenance(ReportId: _report.Id));
    }

    /// <summary>Stages two filings plus the report's Processed status in <paramref name="unitOfWork"/>.</summary>
    private async Task StageProcessedReportAsync(IUnitOfWork unitOfWork, Guid? secondFilingProfileId = null)
    {
        var ct = TestContext.Current.CancellationToken;
        await unitOfWork.Filings.AddAsync(MakeFiling("ACME"), ct);
        await unitOfWork.Filings.AddAsync(MakeFiling("Globex", secondFilingProfileId), ct);

        var report = await unitOfWork.Reports.GetByIdAsync(_report.Id, ct);
        report!.SetStatus(ReportStatus.Processed);
        await unitOfWork.Reports.UpdateAsync(report, ct);
    }

    private async Task<int> CountFilingsAsync()
    {
        await using var verify = new AppDbContext(_options);
        return await verify.Filings.AsNoTracking().CountAsync(TestContext.Current.CancellationToken);
    }

    private async Task<ReportStatus?> ReadReportStatusAsync()
    {
        await using var verify = new AppDbContext(_options);
        return await verify.Reports.AsNoTracking()
            .Where(r => r.Id == _report.Id)
            .Select(r => (ReportStatus?)r.Status)
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SaveChangesAsync_StagedFilingsAndReportStatus_PersistsFilings()
    {
        await using var unitOfWork = _factory.Create();
        await StageProcessedReportAsync(unitOfWork);

        await unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        (await CountFilingsAsync()).Should().Be(2);
    }

    [Fact]
    public async Task SaveChangesAsync_StagedFilingsAndReportStatus_PersistsReportStatus()
    {
        await using var unitOfWork = _factory.Create();
        await StageProcessedReportAsync(unitOfWork);

        await unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        (await ReadReportStatusAsync()).Should().Be(ReportStatus.Processed);
    }

    [Fact]
    public async Task AddAsync_BeforeSaveChanges_DoesNotReachDatabase()
    {
        await using var unitOfWork = _factory.Create();

        await StageProcessedReportAsync(unitOfWork);

        (await CountFilingsAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DisposeAsync_WithoutSaveChanges_DiscardsStagedFilings()
    {
        await using (var unitOfWork = _factory.Create())
        {
            await StageProcessedReportAsync(unitOfWork);
        }

        (await CountFilingsAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DisposeAsync_WithoutSaveChanges_LeavesReportStatusUnchanged()
    {
        await using (var unitOfWork = _factory.Create())
        {
            await StageProcessedReportAsync(unitOfWork);
        }

        (await ReadReportStatusAsync()).Should().Be(ReportStatus.Init);
    }

    // ── Regression: a failure part-way through the commit must not leave partial state ──

    [Fact]
    public async Task SaveChangesAsync_SecondFilingViolatesForeignKey_Throws()
    {
        await using var unitOfWork = _factory.Create();
        await StageProcessedReportAsync(unitOfWork, secondFilingProfileId: Guid.NewGuid());

        var act = () => unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task SaveChangesAsync_SecondFilingViolatesForeignKey_PersistsNoFilings()
    {
        await using var unitOfWork = _factory.Create();
        await StageProcessedReportAsync(unitOfWork, secondFilingProfileId: Guid.NewGuid());

        await FluentActions.Awaiting(() => unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken))
            .Should().ThrowAsync<DbUpdateException>();

        (await CountFilingsAsync()).Should().Be(0, because: "the valid first filing must roll back with the failing one");
    }

    [Fact]
    public async Task SaveChangesAsync_SecondFilingViolatesForeignKey_LeavesReportStatusUnchanged()
    {
        await using var unitOfWork = _factory.Create();
        await StageProcessedReportAsync(unitOfWork, secondFilingProfileId: Guid.NewGuid());

        await FluentActions.Awaiting(() => unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken))
            .Should().ThrowAsync<DbUpdateException>();

        (await ReadReportStatusAsync()).Should().Be(ReportStatus.Init);
    }

    // ── Report deletion: filings and report are removed together ──

    private async Task SeedCommittedFilingsAsync()
    {
        await using var unitOfWork = _factory.Create();
        await StageProcessedReportAsync(unitOfWork);
        await unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SaveChangesAsync_StagedFilingAndReportDeletes_RemovesFilings()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedCommittedFilingsAsync();
        await using var unitOfWork = _factory.Create();
        await unitOfWork.Filings.DeleteByReportIdAsync(_report.Id, ct);
        await unitOfWork.Reports.DeleteAsync(_report.Id, ct);

        await unitOfWork.SaveChangesAsync(ct);

        (await CountFilingsAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SaveChangesAsync_StagedFilingAndReportDeletes_RemovesReport()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedCommittedFilingsAsync();
        await using var unitOfWork = _factory.Create();
        await unitOfWork.Filings.DeleteByReportIdAsync(_report.Id, ct);
        await unitOfWork.Reports.DeleteManyAsync([_report.Id], ct);

        await unitOfWork.SaveChangesAsync(ct);

        (await ReadReportStatusAsync()).Should().BeNull();
    }

    [Fact]
    public async Task DisposeAsync_StagedDeletesWithoutSaveChanges_KeepsFilings()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedCommittedFilingsAsync();

        await using (var unitOfWork = _factory.Create())
        {
            await unitOfWork.Filings.DeleteByReportIdAsync(_report.Id, ct);
            await unitOfWork.Reports.DeleteAsync(_report.Id, ct);
        }

        (await CountFilingsAsync()).Should().Be(2);
    }

    // ── Duplicate detection sees changes staged in the same unit of work ──

    private static readonly DateOnly INCOME_DATE = new(2024, 6, 15);

    [Fact]
    public async Task GetByIncomeEventAsync_FilingStagedInSameUnitOfWork_ReturnsIt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var unitOfWork = _factory.Create();
        await unitOfWork.Filings.AddAsync(MakeFiling("ACME"), ct);

        var found = await unitOfWork.Filings.GetByIncomeEventAsync(_profile.Id, "ACME", INCOME_DATE, ct);

        found.Should().ContainSingle();
    }

    [Fact]
    public async Task GetByIncomeEventAsync_StagedFilingForOtherEntity_IsNotReturned()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var unitOfWork = _factory.Create();
        await unitOfWork.Filings.AddAsync(MakeFiling("Globex"), ct);

        var found = await unitOfWork.Filings.GetByIncomeEventAsync(_profile.Id, "ACME", INCOME_DATE, ct);

        found.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIncomeEventAsync_CommittedFilingStagedForDeletion_IsNotReturned()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedCommittedFilingsAsync();
        await using var unitOfWork = _factory.Create();
        await unitOfWork.Filings.DeleteByReportIdAsync(_report.Id, ct);

        var found = await unitOfWork.Filings.GetByIncomeEventAsync(_profile.Id, "ACME", INCOME_DATE, ct);

        found.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIncomeEventAsync_CommittedFiling_IsReturnedOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedCommittedFilingsAsync();
        await using var unitOfWork = _factory.Create();

        var found = await unitOfWork.Filings.GetByIncomeEventAsync(_profile.Id, "ACME", INCOME_DATE, ct);

        found.Should().ContainSingle();
    }

    [Fact]
    public async Task GetByIncomeEventAsync_CommittedFilingStagedForUpdate_ReturnsTrackedInstanceOnce()
    {
        // The tracked (staged) copy wins over its database row — never both.
        var ct = TestContext.Current.CancellationToken;
        await SeedCommittedFilingsAsync();
        await using var unitOfWork = _factory.Create();
        var tracked = (await unitOfWork.Filings.GetByIncomeEventAsync(_profile.Id, "ACME", INCOME_DATE, ct))[0];
        var staged = await unitOfWork.Filings.GetByIdAsync(tracked.Id, ct);
        await unitOfWork.Filings.UpdateAsync(staged!, ct);

        var found = await unitOfWork.Filings.GetByIncomeEventAsync(_profile.Id, "ACME", INCOME_DATE, ct);

        found.Should().ContainSingle().Which.Should().BeSameAs(staged);
    }

    // ── Updates must not clobber a change already staged for the same entity ──

    [Fact]
    public async Task UpdateAsync_FilingStagedAsInsert_CommitsAsSingleInsert()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var unitOfWork = _factory.Create();
        var filing = MakeFiling("ACME");
        await unitOfWork.Filings.AddAsync(filing, ct);
        await unitOfWork.Filings.UpdateAsync(filing, ct);

        await unitOfWork.SaveChangesAsync(ct);

        (await CountFilingsAsync()).Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_FilingStagedForDeletion_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedCommittedFilingsAsync();
        await using var unitOfWork = _factory.Create();
        var filing = (await unitOfWork.Filings.GetByReportIdAsync(_report.Id, ct))[0];
        await unitOfWork.Filings.DeleteAsync(filing.Id, ct);

        var act = () => unitOfWork.Filings.UpdateAsync(filing, ct);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdateAsync_ReportStagedForDeletion_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var unitOfWork = _factory.Create();
        var report = await unitOfWork.Reports.GetByIdAsync(_report.Id, ct);
        await unitOfWork.Reports.DeleteAsync(_report.Id, ct);

        var act = () => unitOfWork.Reports.UpdateAsync(report!, ct);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── Status-only update: the attachment BLOB is not rewritten ──

    private async Task<Report> LoadDetachedReportAsync()
    {
        await using var read = new AppDbContext(_options);
        return await read.Reports.AsNoTracking().SingleAsync(r => r.Id == _report.Id, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateStatusAsync_DetachedReport_PersistsStatus()
    {
        var ct = TestContext.Current.CancellationToken;
        var report = await LoadDetachedReportAsync();
        report.SetStatus(ReportStatus.Processed);
        await using var unitOfWork = _factory.Create();

        await unitOfWork.Reports.UpdateStatusAsync(report, ct);
        await unitOfWork.SaveChangesAsync(ct);

        (await ReadReportStatusAsync()).Should().Be(ReportStatus.Processed);
    }

    [Fact]
    public async Task UpdateStatusAsync_DetachedReport_MarksOnlyStatusModified()
    {
        var ct = TestContext.Current.CancellationToken;
        var report = await LoadDetachedReportAsync();
        report.SetStatus(ReportStatus.Processed);
        await using var db = new AppDbContext(_options);
        var repository = new Rentier.Infrastructure.Repositories.ReportRepository(db, isStaging: true);

        await repository.UpdateStatusAsync(report, ct);

        db.Entry(report).Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name)
            .Should().Equal(nameof(Report.Status));
    }

    [Fact]
    public async Task UpdateStatusAsync_DetachedReport_DoesNotCopyAttachmentIntoSnapshot()
    {
        var ct = TestContext.Current.CancellationToken;
        var report = await LoadDetachedReportAsync();
        report.SetStatus(ReportStatus.Processed);
        await using var db = new AppDbContext(_options);
        var repository = new Rentier.Infrastructure.Repositories.ReportRepository(db, isStaging: true);

        await repository.UpdateStatusAsync(report, ct);

        db.Entry(report).Property(r => r.AttachmentContent).OriginalValue
            .Should().BeSameAs(report.AttachmentContent);
    }

    [Fact]
    public async Task UpdateStatusAsync_ReportStagedForDeletion_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        var detached = await LoadDetachedReportAsync();
        await using var unitOfWork = _factory.Create();
        await unitOfWork.Reports.DeleteAsync(_report.Id, ct);

        var act = () => unitOfWork.Reports.UpdateStatusAsync(detached, ct);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
